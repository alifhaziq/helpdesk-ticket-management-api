namespace HelpdeskTicketManagement.Application.Dtos.Tickets;

public sealed record TicketAttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    Guid UploadedById,
    string? UploadedByName,
    DateTimeOffset UploadedAt);
