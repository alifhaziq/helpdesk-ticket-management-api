using HelpDeskPro.Api.Extensions;
using HelpDeskPro.Api.Requests;
using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Application.Dtos.Tickets;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TicketsController(
    IHelpDeskProDbContext dbContext,
    ICurrentUserService currentUser,
    IAuditService auditService,
    ISlaPolicy slaPolicy,
    IEmailSender emailSender,
    IFileStorageService fileStorage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TicketResponse>>> GetTickets(
        [FromQuery] TicketStatus? status,
        [FromQuery] bool assignedToMe,
        [FromQuery] bool createdByMe,
        [FromQuery] bool slaBreached,
        [FromQuery] int? slaDueWithinHours,
        CancellationToken cancellationToken)
    {
        var query = BuildDetailedTicketQuery();

        if (status.HasValue)
        {
            query = query.Where(ticket => ticket.Status == status.Value);
        }

        if (assignedToMe && currentUser.UserId is { } assignedUserId)
        {
            query = query.Where(ticket => ticket.AssignedToId == assignedUserId);
        }

        if (createdByMe && currentUser.UserId is { } createdByUserId)
        {
            query = query.Where(ticket => ticket.CreatedById == createdByUserId);
        }

        var now = DateTimeOffset.UtcNow;
        if (slaBreached)
        {
            query = ApplySlaBreachedFilter(query, now);
        }

        if (slaDueWithinHours is > 0)
        {
            query = ApplySlaDueSoonFilter(query, now, now.AddHours(slaDueWithinHours.Value));
        }

        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(tickets.Select(ticket => ticket.ToResponse(slaPolicy)).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> GetTicket(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await BuildDetailedTicketQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return ticket is null ? NotFound() : Ok(ticket.ToResponse(slaPolicy));
    }

    [HttpPost]
    public async Task<ActionResult<TicketResponse>> CreateTicket(
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Title and description are required.");
        }

        var createdAt = DateTimeOffset.UtcNow;
        var slaTargets = slaPolicy.CalculateTargets(createdAt, request.Priority);
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Status = TicketStatus.Open,
            CreatedById = userId,
            CreatedAt = createdAt,
            FirstResponseDueAt = slaTargets.FirstResponseDueAt,
            ResolutionDueAt = slaTargets.ResolutionDueAt
        };

        dbContext.Tickets.Add(ticket);
        await auditService.RecordAsync(
            "Ticket.Create",
            nameof(Ticket),
            ticket.Id,
            new { ticket.Title, ticket.Priority },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, created.ToResponse(slaPolicy));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> UpdateTicket(
        Guid id,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (ticket is null || !CanAccess(ticket))
        {
            return NotFound();
        }

        if (!CanEditTicketDetails(ticket))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest("Title and description are required.");
        }

        var previousPriority = ticket.Priority;
        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.Priority = request.Priority;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (previousPriority != request.Priority)
        {
            ApplySlaTargets(ticket);
        }

        await auditService.RecordAsync(
            "Ticket.Update",
            nameof(Ticket),
            ticket.Id,
            new { ticket.Title, PreviousPriority = previousPriority, ticket.Priority },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return Ok(updated.ToResponse(slaPolicy));
    }

    [Authorize(Roles = "Admin,Agent")]
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<TicketResponse>> AssignTicket(
        Guid id,
        AssignTicketRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (ticket is null || !CanAccess(ticket))
        {
            return NotFound();
        }

        var assignee = await dbContext.Users.FirstOrDefaultAsync(
            user => user.Id == request.AgentId && user.IsActive,
            cancellationToken);

        if (assignee is null || assignee.Role != UserRole.Agent)
        {
            return BadRequest("Assignee must be an active agent.");
        }

        if (currentUser.Role == UserRole.Agent)
        {
            if (request.AgentId != currentUser.UserId)
            {
                return Forbid();
            }

            if (ticket.AssignedToId is not null && ticket.AssignedToId != currentUser.UserId)
            {
                return Forbid();
            }
        }

        var previousAssigneeId = ticket.AssignedToId;
        ticket.AssignedToId = assignee.Id;
        ticket.Status = ticket.Status == TicketStatus.Open ? TicketStatus.InProgress : ticket.Status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        MarkFirstResponse(ticket, ticket.UpdatedAt.Value);

        await auditService.RecordAsync(
            "Ticket.Assign",
            nameof(Ticket),
            ticket.Id,
            new { PreviousAssigneeId = previousAssigneeId, AssigneeId = assignee.Id },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await emailSender.TicketAssignedAsync(ticket, assignee, cancellationToken);

        var updated = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return Ok(updated.ToResponse(slaPolicy));
    }

    [Authorize(Roles = "Admin,Agent")]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TicketResponse>> UpdateStatus(
        Guid id,
        UpdateTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (ticket is null || !CanAccess(ticket))
        {
            return NotFound();
        }

        if (currentUser.Role == UserRole.Agent &&
            ticket.AssignedToId is not null &&
            ticket.AssignedToId != currentUser.UserId)
        {
            return Forbid();
        }

        var now = DateTimeOffset.UtcNow;
        var previousStatus = ticket.Status;
        ticket.Status = request.Status;
        ticket.UpdatedAt = now;
        MarkFirstResponse(ticket, now);

        if (request.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.ResolvedAt ??= now;
        }
        else
        {
            ticket.ResolvedAt = null;
        }

        ticket.ClosedAt = request.Status == TicketStatus.Closed ? now : null;

        await auditService.RecordAsync(
            "Ticket.StatusChanged",
            nameof(Ticket),
            ticket.Id,
            new { PreviousStatus = previousStatus, ticket.Status },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await emailSender.TicketStatusChangedAsync(ticket, cancellationToken);

        var updated = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return Ok(updated.ToResponse(slaPolicy));
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<TicketCommentResponse>> AddComment(
        Guid id,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (ticket is null || !CanAccess(ticket))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Comment message is required.");
        }

        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            AuthorId = userId,
            Message = request.Message.Trim()
        };

        dbContext.TicketComments.Add(comment);
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        if (currentUser.Role is UserRole.Admin or UserRole.Agent)
        {
            MarkFirstResponse(ticket, ticket.UpdatedAt.Value);
        }

        await auditService.RecordAsync(
            "Ticket.Comment",
            nameof(Ticket),
            ticket.Id,
            new { CommentId = comment.Id },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await emailSender.TicketCommentedAsync(ticket, comment, cancellationToken);

        var author = await dbContext.Users.AsNoTracking().FirstAsync(user => user.Id == userId, cancellationToken);
        var response = new TicketCommentResponse(
            comment.Id,
            comment.Message,
            comment.AuthorId,
            author.FullName,
            comment.CreatedAt);

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, response);
    }

    [HttpPost("{ticketId}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadAttachment(
        Guid ticketId,
        [FromForm] UploadAttachmentRequest request)
    {
        var cancellationToken = HttpContext.RequestAborted;
        var file = request.File;

        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var ticket = await dbContext.Tickets.FirstOrDefaultAsync(candidate => candidate.Id == ticketId, cancellationToken);
        if (ticket is null || !CanAccess(ticket))
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Attachment file is required.");
        }

        await using var fileStream = file.OpenReadStream();
        var storedFile = await fileStorage.SaveAsync(
            new FileUpload(
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                file.Length,
                fileStream),
            cancellationToken);

        var attachment = new TicketAttachment
        {
            TicketId = ticket.Id,
            UploadedById = userId,
            FileName = storedFile.FileName,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Size = file.Length,
            StoragePath = storedFile.StoragePath
        };

        dbContext.TicketAttachments.Add(attachment);
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await auditService.RecordAsync(
            "Ticket.AttachmentUploaded",
            nameof(Ticket),
            ticket.Id,
            new { AttachmentId = attachment.Id, attachment.OriginalFileName, attachment.Size },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var uploader = await dbContext.Users.AsNoTracking().FirstAsync(user => user.Id == userId, cancellationToken);
        var response = new TicketAttachmentResponse(
            attachment.Id,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.Size,
            attachment.UploadedById,
            uploader.FullName,
            attachment.UploadedAt);

        return CreatedAtAction(
            nameof(DownloadAttachment),
            new { id = ticket.Id, attachmentId = attachment.Id },
            response);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadAttachment(
        Guid id,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var ticket = await dbContext.Tickets
            .Include(candidate => candidate.Attachments)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (ticket is null || !CanAccess(ticket))
        {
            return NotFound();
        }

        var attachment = ticket.Attachments.FirstOrDefault(candidate => candidate.Id == attachmentId);
        if (attachment is null)
        {
            return NotFound();
        }

        var stream = await fileStorage.OpenReadAsync(attachment.StoragePath, cancellationToken);
        return stream is null
            ? NotFound()
            : File(stream, attachment.ContentType, attachment.OriginalFileName);
    }

    private IQueryable<Ticket> BuildDetailedTicketQuery()
    {
        return ApplyAccessScope(dbContext.Tickets)
            .Include(ticket => ticket.CreatedBy)
            .Include(ticket => ticket.AssignedTo)
            .Include(ticket => ticket.Comments)
            .ThenInclude(comment => comment.Author)
            .Include(ticket => ticket.Attachments)
            .ThenInclude(attachment => attachment.UploadedBy);
    }

    private IQueryable<Ticket> ApplyAccessScope(IQueryable<Ticket> query)
    {
        if (currentUser.UserId is not { } userId)
        {
            return query.Where(ticket => false);
        }

        return currentUser.Role switch
        {
            UserRole.Admin => query,
            UserRole.Agent => query.Where(ticket =>
                ticket.AssignedToId == userId ||
                ticket.AssignedToId == null ||
                ticket.CreatedById == userId),
            _ => query.Where(ticket => ticket.CreatedById == userId)
        };
    }

    private static IQueryable<Ticket> ApplySlaBreachedFilter(IQueryable<Ticket> query, DateTimeOffset now)
    {
        return query.Where(ticket =>
            (ticket.FirstResponseDueAt != default &&
             ((ticket.FirstResponseAt == null && ticket.FirstResponseDueAt < now) ||
              (ticket.FirstResponseAt != null && ticket.FirstResponseAt > ticket.FirstResponseDueAt))) ||
            (ticket.ResolutionDueAt != default &&
             ((ticket.ResolvedAt == null &&
               ticket.Status != TicketStatus.Resolved &&
               ticket.Status != TicketStatus.Closed &&
               ticket.ResolutionDueAt < now) ||
              (ticket.ResolvedAt != null && ticket.ResolvedAt > ticket.ResolutionDueAt))));
    }

    private static IQueryable<Ticket> ApplySlaDueSoonFilter(
        IQueryable<Ticket> query,
        DateTimeOffset now,
        DateTimeOffset dueBy)
    {
        return query.Where(ticket =>
            (ticket.FirstResponseDueAt != default &&
             ticket.FirstResponseAt == null &&
             ticket.FirstResponseDueAt >= now &&
             ticket.FirstResponseDueAt <= dueBy) ||
            (ticket.ResolutionDueAt != default &&
             ticket.ResolvedAt == null &&
             ticket.Status != TicketStatus.Resolved &&
             ticket.Status != TicketStatus.Closed &&
             ticket.ResolutionDueAt >= now &&
             ticket.ResolutionDueAt <= dueBy));
    }

    private void ApplySlaTargets(Ticket ticket)
    {
        var targets = slaPolicy.CalculateTargets(ticket.CreatedAt, ticket.Priority);
        ticket.FirstResponseDueAt = targets.FirstResponseDueAt;
        ticket.ResolutionDueAt = targets.ResolutionDueAt;
    }

    private void MarkFirstResponse(Ticket ticket, DateTimeOffset respondedAt)
    {
        if (ticket.FirstResponseAt is null && currentUser.Role is UserRole.Admin or UserRole.Agent)
        {
            ticket.FirstResponseAt = respondedAt;
        }
    }

    private bool CanAccess(Ticket ticket)
    {
        if (currentUser.UserId is not { } userId)
        {
            return false;
        }

        return currentUser.Role switch
        {
            UserRole.Admin => true,
            UserRole.Agent => ticket.AssignedToId == userId ||
                              ticket.AssignedToId == null ||
                              ticket.CreatedById == userId,
            _ => ticket.CreatedById == userId
        };
    }

    private bool CanEditTicketDetails(Ticket ticket)
    {
        if (currentUser.UserId is not { } userId)
        {
            return false;
        }

        return currentUser.Role is UserRole.Admin or UserRole.Agent ||
               (ticket.CreatedById == userId && ticket.Status is TicketStatus.Open or TicketStatus.InProgress);
    }
}
