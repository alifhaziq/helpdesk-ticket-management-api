using HelpdeskTicketManagement.Domain.Entities;

namespace HelpdeskTicketManagement.Application.Abstractions;

public interface IEmailSender
{
    Task TicketAssignedAsync(Ticket ticket, AppUser assignee, CancellationToken cancellationToken = default);
    Task TicketStatusChangedAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task TicketCommentedAsync(Ticket ticket, TicketComment comment, CancellationToken cancellationToken = default);
}
