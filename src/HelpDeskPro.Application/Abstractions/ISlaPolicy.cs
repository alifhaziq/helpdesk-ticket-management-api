using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Application.Abstractions;

public interface ISlaPolicy
{
    SlaTargets CalculateTargets(DateTimeOffset createdAt, TicketPriority priority);
    bool IsResponseBreached(Ticket ticket, DateTimeOffset now);
    bool IsResolutionBreached(Ticket ticket, DateTimeOffset now);
    bool IsBreached(Ticket ticket, DateTimeOffset now);
}

public sealed record SlaTargets(DateTimeOffset FirstResponseDueAt, DateTimeOffset ResolutionDueAt);
