using HelpdeskTicketManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskTicketManagement.Application.Abstractions;

public interface IHelpdeskDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
