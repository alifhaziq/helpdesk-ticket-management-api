using HelpdeskTicketManagement.Api.Extensions;
using HelpdeskTicketManagement.Application.Abstractions;
using HelpdeskTicketManagement.Application.Dtos.Dashboard;
using HelpdeskTicketManagement.Domain.Entities;
using HelpdeskTicketManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskTicketManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class DashboardController(
    IHelpdeskDbContext dbContext,
    ICurrentUserService currentUser) : ControllerBase
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
            statusCounts.Select(item => new StatusCountResponse(item.Status.ToString(), item.Count)).ToArray(),
            priorityCounts.Select(item => new PriorityCountResponse(item.Priority.ToString(), item.Count)).ToArray(),
            recentTickets.Select(ticket => ticket.ToResponse()).ToArray());

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
}
