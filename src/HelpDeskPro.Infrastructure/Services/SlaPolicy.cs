using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Infrastructure.Services;

public sealed class SlaPolicy : ISlaPolicy
{
    public SlaTargets CalculateTargets(DateTimeOffset createdAt, TicketPriority priority)
    {
        var (firstResponse, resolution) = priority switch
        {
            TicketPriority.Critical => (TimeSpan.FromHours(1), TimeSpan.FromHours(8)),
            TicketPriority.High => (TimeSpan.FromHours(4), TimeSpan.FromDays(1)),
            TicketPriority.Low => (TimeSpan.FromDays(2), TimeSpan.FromDays(5)),
            _ => (TimeSpan.FromDays(1), TimeSpan.FromDays(3))
        };

        return new SlaTargets(createdAt.Add(firstResponse), createdAt.Add(resolution));
    }

    public bool IsResponseBreached(Ticket ticket, DateTimeOffset now)
    {
        if (ticket.FirstResponseDueAt == default)
        {
            return false;
        }

        return ticket.FirstResponseAt is { } firstResponseAt
            ? firstResponseAt > ticket.FirstResponseDueAt
            : now > ticket.FirstResponseDueAt;
    }

    public bool IsResolutionBreached(Ticket ticket, DateTimeOffset now)
    {
        if (ticket.ResolutionDueAt == default)
        {
            return false;
        }

        if (ticket.ResolvedAt is { } resolvedAt)
        {
            return resolvedAt > ticket.ResolutionDueAt;
        }

        return ticket.Status is not TicketStatus.Resolved and not TicketStatus.Closed &&
               now > ticket.ResolutionDueAt;
    }

    public bool IsBreached(Ticket ticket, DateTimeOffset now) =>
        IsResponseBreached(ticket, now) || IsResolutionBreached(ticket, now);
}
