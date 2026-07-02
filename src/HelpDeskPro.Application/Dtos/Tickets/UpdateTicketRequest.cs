using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Application.Dtos.Tickets;

public sealed record UpdateTicketRequest(string Title, string Description, TicketPriority Priority);
