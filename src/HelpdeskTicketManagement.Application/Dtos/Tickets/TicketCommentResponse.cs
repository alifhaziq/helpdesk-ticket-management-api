namespace HelpdeskTicketManagement.Application.Dtos.Tickets;

public sealed record TicketCommentResponse(
    Guid Id,
    string Message,
    Guid AuthorId,
    string? AuthorName,
    DateTimeOffset CreatedAt);
