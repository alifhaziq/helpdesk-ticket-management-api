using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Application.Dtos.Tickets;

public sealed record CreateTicketRequest(string Title, string Description, TicketPriority Priority = TicketPriority.Medium);
