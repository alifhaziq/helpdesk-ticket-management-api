using HelpdeskTicketManagement.Domain.Enums;

namespace HelpdeskTicketManagement.Application.Dtos.Tickets;

public sealed record CreateTicketRequest(string Title, string Description, TicketPriority Priority = TicketPriority.Medium);
