using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Application.Dtos.Tickets;

public sealed record UpdateTicketStatusRequest(TicketStatus Status);
