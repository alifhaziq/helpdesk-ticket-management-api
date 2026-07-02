using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace HelpDeskPro.Infrastructure.Services;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task TicketAssignedAsync(Ticket ticket, AppUser assignee, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email notification: ticket {TicketId} assigned to {AssigneeEmail}.",
            ticket.Id,
            assignee.Email);

        return Task.CompletedTask;
    }

    public Task TicketStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email notification: ticket {TicketId} status changed to {Status}.",
            ticket.Id,
            ticket.Status);

        return Task.CompletedTask;
    }

    public Task TicketCommentedAsync(Ticket ticket, TicketComment comment, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email notification: ticket {TicketId} received comment {CommentId}.",
            ticket.Id,
            comment.Id);

        return Task.CompletedTask;
    }
}
