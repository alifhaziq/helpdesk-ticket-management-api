using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Infrastructure.Services;
using Xunit;

namespace HelpDeskPro.Tests;

public sealed class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void VerifyPassword_ReturnsTrueOnlyForOriginalPassword()
    {
        var user = new AppUser { Email = "user@example.com" };
        var hasher = new Pbkdf2PasswordHasher();

        user.PasswordHash = hasher.HashPassword(user, "CorrectHorseBatteryStaple");

        Assert.True(hasher.VerifyPassword(user, "CorrectHorseBatteryStaple"));
        Assert.False(hasher.VerifyPassword(user, "wrong-password"));
    }

    [Fact]
    public void VerifyPassword_ReturnsFalseForMalformedHash()
    {
        var user = new AppUser { PasswordHash = "not-a-valid-hash" };
        var hasher = new Pbkdf2PasswordHasher();

        Assert.False(hasher.VerifyPassword(user, "anything"));
    }
}
