using System.Net;
using System.Net.Mail;
using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelpDeskPro.Infrastructure.Services;

public sealed class SmtpEmailSender(
    IHelpDeskProDbContext dbContext,
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task TicketAssignedAsync(Ticket ticket, AppUser assignee, CancellationToken cancellationToken = default)
    {
        var subject = $"Ticket assigned: {ticket.Title}";
        var body = $"""
            Ticket {ticket.Id} has been assigned to {assignee.FullName}.

            Title: {ticket.Title}
            Priority: {ticket.Priority}
            Status: {ticket.Status}
            """;

        return SendAsync([assignee.Email], subject, body, cancellationToken);
    }

    public async Task TicketStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        var recipients = await GetTicketRecipientEmailsAsync(ticket, cancellationToken);
        var subject = $"Ticket status changed: {ticket.Title}";
        var body = $"""
            Ticket {ticket.Id} status changed to {ticket.Status}.

            Title: {ticket.Title}
            Priority: {ticket.Priority}
            """;

        await SendAsync(recipients, subject, body, cancellationToken);
    }

    public async Task TicketCommentedAsync(Ticket ticket, TicketComment comment, CancellationToken cancellationToken = default)
    {
        var recipients = await GetTicketRecipientEmailsAsync(ticket, cancellationToken);
        var subject = $"New comment on ticket: {ticket.Title}";
        var body = $"""
            Ticket {ticket.Id} received a new comment.

            Title: {ticket.Title}
            Comment: {comment.Message}
            """;

        await SendAsync(recipients, subject, body, cancellationToken);
    }

    private async Task<IReadOnlyCollection<string>> GetTicketRecipientEmailsAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        var userIds = new[] { ticket.CreatedById, ticket.AssignedToId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (userIds.Length == 0)
        {
            return [];
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.IsActive)
            .Select(user => user.Email)
            .ToArrayAsync(cancellationToken);
    }

    private async Task SendAsync(
        IEnumerable<string> recipientEmails,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var smtpOptions = options.Value;
        var recipients = recipientEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            logger.LogInformation("Email notification skipped because there are no recipients for {Subject}.", subject);
            return;
        }

        if (!smtpOptions.Enabled)
        {
            logger.LogInformation(
                "SMTP disabled; email notification '{Subject}' would be sent to {Recipients}.",
                subject,
                string.Join(", ", recipients));
            return;
        }

        if (string.IsNullOrWhiteSpace(smtpOptions.Host) || string.IsNullOrWhiteSpace(smtpOptions.FromEmail))
        {
            logger.LogWarning("SMTP is enabled, but Host or FromEmail is missing.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(smtpOptions.FromEmail, smtpOptions.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        using var client = new SmtpClient(smtpOptions.Host, smtpOptions.Port)
        {
            EnableSsl = smtpOptions.UseSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(smtpOptions.UserName))
        {
            client.Credentials = new NetworkCredential(smtpOptions.UserName, smtpOptions.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
