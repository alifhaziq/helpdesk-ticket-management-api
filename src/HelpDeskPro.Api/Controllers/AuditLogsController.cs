using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Application.Dtos.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/audit-logs")]
public sealed class AuditLogsController(IHelpDeskProDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AuditLogResponse>>> GetAuditLogs(
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(log => log.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(log => log.EntityName == entityName);
        }

        if (entityId.HasValue)
        {
            query = query.Where(log => log.EntityId == entityId.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(log => log.UserId == userId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= to.Value);
        }

        var limit = Math.Clamp(take, 1, 500);
        var logs = await query
            .OrderByDescending(log => log.CreatedAt)
            .Take(limit)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.UserId,
                log.User == null ? null : log.User.FullName,
                log.User == null ? null : log.User.Email,
                log.Action,
                log.EntityName,
                log.EntityId,
                log.Details,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }
}
