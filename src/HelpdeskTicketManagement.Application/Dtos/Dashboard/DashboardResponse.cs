using HelpdeskTicketManagement.Application.Dtos.Tickets;

namespace HelpdeskTicketManagement.Application.Dtos.Dashboard;

public sealed record DashboardResponse(
    int TotalTickets,
    int OpenTickets,
    int InProgressTickets,
    int ResolvedTickets,
    int ClosedTickets,
    int UnassignedTickets,
    int TicketsAssignedToMe,
    int TicketsCreatedByMe,
    IReadOnlyCollection<StatusCountResponse> ByStatus,
    IReadOnlyCollection<PriorityCountResponse> ByPriority,
    IReadOnlyCollection<TicketResponse> RecentTickets);

public sealed record StatusCountResponse(string Status, int Count);

public sealed record PriorityCountResponse(string Priority, int Count);
