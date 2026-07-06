using HelpDeskPro.Api.Extensions;
using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Application.Dtos.Dashboard;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DashboardController(
    IHelpDeskProDbContext dbContext,
    ICurrentUserService currentUser,
    ISlaPolicy slaPolicy) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(CancellationToken cancellationToken)
    {
        var scopedTickets = ApplyAccessScope(dbContext.Tickets);

        var totalTickets = await scopedTickets.CountAsync(cancellationToken);
        var openTickets = await scopedTickets.CountAsync(ticket => ticket.Status == TicketStatus.Open, cancellationToken);
        var inProgressTickets = await scopedTickets.CountAsync(ticket => ticket.Status == TicketStatus.InProgress, cancellationToken);
        var resolvedTickets = await scopedTickets.CountAsync(ticket => ticket.Status == TicketStatus.Resolved, cancellationToken);
        var closedTickets = await scopedTickets.CountAsync(ticket => ticket.Status == TicketStatus.Closed, cancellationToken);
        var unassignedTickets = await scopedTickets.CountAsync(ticket => ticket.AssignedToId == null, cancellationToken);

        var userId = currentUser.UserId;
        var assignedToMe = userId.HasValue
            ? await scopedTickets.CountAsync(ticket => ticket.AssignedToId == userId.Value, cancellationToken)
            : 0;
        var createdByMe = userId.HasValue
            ? await scopedTickets.CountAsync(ticket => ticket.CreatedById == userId.Value, cancellationToken)
            : 0;
        var now = DateTimeOffset.UtcNow;
        var dueSoonAt = now.AddHours(24);
        var slaBreachedTickets = await ApplySlaBreachedFilter(scopedTickets, now).CountAsync(cancellationToken);
        var slaDueSoonTickets = await ApplySlaDueSoonFilter(scopedTickets, now, dueSoonAt).CountAsync(cancellationToken);

        var statusCounts = await scopedTickets
            .GroupBy(ticket => ticket.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var priorityCounts = await scopedTickets
            .GroupBy(ticket => ticket.Priority)
            .Select(group => new { Priority = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var recentTickets = await ApplyAccessScope(dbContext.Tickets)
            .Include(ticket => ticket.CreatedBy)
            .Include(ticket => ticket.AssignedTo)
            .Include(ticket => ticket.Comments)
            .ThenInclude(comment => comment.Author)
            .Include(ticket => ticket.Attachments)
            .ThenInclude(attachment => attachment.UploadedBy)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var response = new DashboardResponse(
            totalTickets,
            openTickets,
            inProgressTickets,
            resolvedTickets,
            closedTickets,
            unassignedTickets,
            assignedToMe,
            createdByMe,
            slaBreachedTickets,
            slaDueSoonTickets,
            statusCounts.Select(item => new StatusCountResponse(item.Status.ToString(), item.Count)).ToArray(),
            priorityCounts.Select(item => new PriorityCountResponse(item.Priority.ToString(), item.Count)).ToArray(),
            recentTickets.Select(ticket => ticket.ToResponse(slaPolicy)).ToArray());

        return Ok(response);
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
}
