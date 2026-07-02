using HelpDeskPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Application.Abstractions;

public interface IHelpDeskProDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
