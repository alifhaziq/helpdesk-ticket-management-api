using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HelpDeskPro.Infrastructure.Data;

public sealed class HelpDeskProDbContext(DbContextOptions<HelpDeskProDbContext> options)
    : DbContext(options), IHelpDeskProDbContext
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var roleConverter = new EnumToStringConverter<UserRole>();
        var statusConverter = new EnumToStringConverter<TicketStatus>();
        var priorityConverter = new EnumToStringConverter<TicketPriority>();

        modelBuilder.Entity<AppUser>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(user => user.Id);
            builder.Property(user => user.FullName).HasMaxLength(150).IsRequired();
            builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
            builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            builder.Property(user => user.Role).HasConversion(roleConverter).HasMaxLength(32).IsRequired();
            builder.HasIndex(user => user.Email).IsUnique();

            builder.HasMany(user => user.CreatedTickets)
                .WithOne(ticket => ticket.CreatedBy)
                .HasForeignKey(ticket => ticket.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(user => user.AssignedTickets)
                .WithOne(ticket => ticket.AssignedTo)
                .HasForeignKey(ticket => ticket.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Ticket>(builder =>
        {
            builder.ToTable("Tickets");
            builder.HasKey(ticket => ticket.Id);
            builder.Property(ticket => ticket.Title).HasMaxLength(200).IsRequired();
            builder.Property(ticket => ticket.Description).HasMaxLength(4000).IsRequired();
            builder.Property(ticket => ticket.Status).HasConversion(statusConverter).HasMaxLength(32).IsRequired();
            builder.Property(ticket => ticket.Priority).HasConversion(priorityConverter).HasMaxLength(32).IsRequired();
            builder.HasIndex(ticket => ticket.Status);
            builder.HasIndex(ticket => ticket.Priority);
            builder.HasIndex(ticket => ticket.CreatedAt);
            builder.HasIndex(ticket => ticket.AssignedToId);
        });

        modelBuilder.Entity<TicketComment>(builder =>
        {
            builder.ToTable("TicketComments");
            builder.HasKey(comment => comment.Id);
            builder.Property(comment => comment.Message).HasMaxLength(2000).IsRequired();
            builder.HasIndex(comment => comment.CreatedAt);

            builder.HasOne(comment => comment.Ticket)
                .WithMany(ticket => ticket.Comments)
                .HasForeignKey(comment => comment.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(comment => comment.Author)
                .WithMany(user => user.Comments)
                .HasForeignKey(comment => comment.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketAttachment>(builder =>
        {
            builder.ToTable("TicketAttachments");
            builder.HasKey(attachment => attachment.Id);
            builder.Property(attachment => attachment.FileName).HasMaxLength(255).IsRequired();
            builder.Property(attachment => attachment.OriginalFileName).HasMaxLength(255).IsRequired();
            builder.Property(attachment => attachment.ContentType).HasMaxLength(100).IsRequired();
            builder.Property(attachment => attachment.StoragePath).HasMaxLength(500).IsRequired();
            builder.HasIndex(attachment => attachment.UploadedAt);

            builder.HasOne(attachment => attachment.Ticket)
                .WithMany(ticket => ticket.Attachments)
                .HasForeignKey(attachment => attachment.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(attachment => attachment.UploadedBy)
                .WithMany(user => user.Attachments)
                .HasForeignKey(attachment => attachment.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
