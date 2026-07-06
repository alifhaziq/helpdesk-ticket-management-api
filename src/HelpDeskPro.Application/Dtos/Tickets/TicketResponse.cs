using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Application.Dtos.Tickets;

public sealed record TicketResponse(
    Guid Id,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    Guid CreatedById,
    string? CreatedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset FirstResponseDueAt,
    DateTimeOffset ResolutionDueAt,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    bool IsResponseSlaBreached,
    bool IsResolutionSlaBreached,
    bool IsSlaBreached,
    IReadOnlyCollection<TicketCommentResponse> Comments,
    IReadOnlyCollection<TicketAttachmentResponse> Attachments);
