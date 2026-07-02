using HelpdeskTicketManagement.Domain.Enums;

namespace HelpdeskTicketManagement.Application.Dtos.Tickets;

public sealed record UpdateTicketRequest(string Title, string Description, TicketPriority Priority);
