using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Application.Dtos.Tickets;
using HelpDeskPro.Domain.Entities;

namespace HelpDeskPro.Api.Extensions;

public static class TicketMappingExtensions
{
    public static TicketResponse ToResponse(this Ticket ticket, ISlaPolicy slaPolicy)
    {
        var now = DateTimeOffset.UtcNow;
        var isResponseSlaBreached = slaPolicy.IsResponseBreached(ticket, now);
        var isResolutionSlaBreached = slaPolicy.IsResolutionBreached(ticket, now);

        var comments = ticket.Comments
            .OrderBy(comment => comment.CreatedAt)
            .Select(comment => new TicketCommentResponse(
                comment.Id,
                comment.Message,
                comment.AuthorId,
                comment.Author?.FullName,
                comment.CreatedAt))
            .ToArray();

        var attachments = ticket.Attachments
            .OrderBy(attachment => attachment.UploadedAt)
            .Select(attachment => new TicketAttachmentResponse(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.Size,
                attachment.UploadedById,
                attachment.UploadedBy?.FullName,
                attachment.UploadedAt))
            .ToArray();

        return new TicketResponse(
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Status,
            ticket.Priority,
            ticket.CreatedById,
            ticket.CreatedBy?.FullName,
            ticket.AssignedToId,
            ticket.AssignedTo?.FullName,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.FirstResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.FirstResponseAt,
            ticket.ResolvedAt,
            ticket.ClosedAt,
            isResponseSlaBreached,
            isResolutionSlaBreached,
            isResponseSlaBreached || isResolutionSlaBreached,
            comments,
            attachments);
    }
}
