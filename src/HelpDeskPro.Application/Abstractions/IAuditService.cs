namespace HelpDeskPro.Application.Abstractions;

public interface IAuditService
{
    Task RecordAsync(
        string action,
        string entityName,
        Guid? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default);
}
