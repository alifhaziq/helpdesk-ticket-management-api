using HelpdeskTicketManagement.Api.Extensions;
using HelpdeskTicketManagement.Api.Requests;
using HelpdeskTicketManagement.Application.Abstractions;
using HelpdeskTicketManagement.Application.Dtos.Tickets;
using HelpdeskTicketManagement.Domain.Entities;
using HelpdeskTicketManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskTicketManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TicketsController(
    IHelpdeskDbContext dbContext,
    ICurrentUserService currentUser,
    IEmailSender emailSender,
    IFileStorageService fileStorage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TicketResponse>>> GetTickets(
        [FromQuery] TicketStatus? status,
        [FromQuery] bool assignedToMe,
        [FromQuery] bool createdByMe,
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

        var tickets = await query
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(tickets.Select(ticket => ticket.ToResponse()).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> GetTicket(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await BuildDetailedTicketQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return ticket is null ? NotFound() : Ok(ticket.ToResponse());
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

        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Status = TicketStatus.Open,
            CreatedById = userId
        };

        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        var created = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, created.ToResponse());
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

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        ticket.Priority = request.Priority;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return Ok(updated.ToResponse());
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

        ticket.AssignedToId = assignee.Id;
        ticket.Status = ticket.Status == TicketStatus.Open ? TicketStatus.InProgress : ticket.Status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await emailSender.TicketAssignedAsync(ticket, assignee, cancellationToken);

        var updated = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return Ok(updated.ToResponse());
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

        ticket.Status = request.Status;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        ticket.ClosedAt = request.Status == TicketStatus.Closed ? DateTimeOffset.UtcNow : null;

        await dbContext.SaveChangesAsync(cancellationToken);
        await emailSender.TicketStatusChangedAsync(ticket, cancellationToken);

        var updated = await BuildDetailedTicketQuery()
            .FirstAsync(candidate => candidate.Id == ticket.Id, cancellationToken);

        return Ok(updated.ToResponse());
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
