using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using HelpDeskPro.Infrastructure.Services;
using Xunit;

namespace HelpDeskPro.Tests;

public sealed class SlaPolicyTests
{
    private readonly SlaPolicy policy = new();

    [Fact]
    public void CalculateTargets_UsesPriorityDurations()
    {
        var createdAt = new DateTimeOffset(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);

        var critical = policy.CalculateTargets(createdAt, TicketPriority.Critical);
        var high = policy.CalculateTargets(createdAt, TicketPriority.High);
        var medium = policy.CalculateTargets(createdAt, TicketPriority.Medium);
        var low = policy.CalculateTargets(createdAt, TicketPriority.Low);

        Assert.Equal(createdAt.AddHours(1), critical.FirstResponseDueAt);
        Assert.Equal(createdAt.AddHours(8), critical.ResolutionDueAt);
        Assert.Equal(createdAt.AddHours(4), high.FirstResponseDueAt);
        Assert.Equal(createdAt.AddDays(1), high.ResolutionDueAt);
        Assert.Equal(createdAt.AddDays(1), medium.FirstResponseDueAt);
        Assert.Equal(createdAt.AddDays(3), medium.ResolutionDueAt);
        Assert.Equal(createdAt.AddDays(2), low.FirstResponseDueAt);
        Assert.Equal(createdAt.AddDays(5), low.ResolutionDueAt);
    }

    [Fact]
    public void IsResponseBreached_ReturnsFalse_WhenFirstResponseWasOnTime()
    {
        var dueAt = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        var ticket = new Ticket
        {
            FirstResponseDueAt = dueAt,
            FirstResponseAt = dueAt.AddMinutes(-5),
            ResolutionDueAt = dueAt.AddDays(1)
        };

        Assert.False(policy.IsResponseBreached(ticket, dueAt.AddHours(2)));
    }

    [Fact]
    public void IsBreached_ReturnsTrue_WhenOpenTicketMissedResolutionDueDate()
    {
        var now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        var ticket = new Ticket
        {
            Status = TicketStatus.Open,
            FirstResponseDueAt = now.AddHours(1),
            ResolutionDueAt = now.AddMinutes(-1)
        };

        Assert.True(policy.IsBreached(ticket, now));
    }
}
