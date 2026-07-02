using HelpdeskTicketManagement.Domain.Enums;

namespace HelpdeskTicketManagement.Application.Dtos.Tickets;

public sealed record UpdateTicketStatusRequest(TicketStatus Status);
