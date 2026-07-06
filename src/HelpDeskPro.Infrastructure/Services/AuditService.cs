using System.Text.Json;
using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Domain.Entities;

namespace HelpDeskPro.Infrastructure.Services;

public sealed class AuditService(
    IHelpDeskProDbContext dbContext,
    ICurrentUserService currentUser) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task RecordAsync(
        string action,
        string entityName,
        Guid? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details is null ? null : JsonSerializer.Serialize(details, JsonOptions)
        });

        return Task.CompletedTask;
    }
}
