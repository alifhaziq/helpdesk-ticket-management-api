namespace HelpDeskPro.Application.Dtos.Audit;

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string? UserEmail,
    string Action,
    string EntityName,
    Guid? EntityId,
    string? Details,
    DateTimeOffset CreatedAt);
