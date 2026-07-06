using HelpDeskPro.Application.Dtos.Tickets;

namespace HelpDeskPro.Application.Dtos.Dashboard;

public sealed record DashboardResponse(
    int TotalTickets,
    int OpenTickets,
    int InProgressTickets,
    int ResolvedTickets,
    int ClosedTickets,
    int UnassignedTickets,
    int TicketsAssignedToMe,
    int TicketsCreatedByMe,
    int SlaBreachedTickets,
    int SlaDueSoonTickets,
    IReadOnlyCollection<StatusCountResponse> ByStatus,
    IReadOnlyCollection<PriorityCountResponse> ByPriority,
    IReadOnlyCollection<TicketResponse> RecentTickets);

public sealed record StatusCountResponse(string Status, int Count);

public sealed record PriorityCountResponse(string Priority, int Count);
