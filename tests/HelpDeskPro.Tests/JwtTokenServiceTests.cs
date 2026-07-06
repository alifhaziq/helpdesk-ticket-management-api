using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using HelpDeskPro.Infrastructure.Options;
using HelpDeskPro.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace HelpDeskPro.Tests;

public sealed class JwtTokenServiceTests
{
    private const string Secret = "12345678901234567890123456789012";

    [Fact]
    public void GenerateAccessToken_IncludesUserIdentityClaims()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "Ada Lovelace",
            Email = "ada@example.com",
            Role = UserRole.Agent
        };
        var service = CreateService();

        var token = service.GenerateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == user.Email);
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == user.Id.ToString());
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == UserRole.Agent.ToString());
    }

    [Fact]
    public void RefreshTokens_AreUniqueAndHashedDeterministically()
    {
        var service = CreateService();

        var first = service.GenerateRefreshToken();
        var second = service.GenerateRefreshToken();

        Assert.NotEqual(first, second);
        Assert.NotEqual(first, service.HashRefreshToken(first));
        Assert.Equal(service.HashRefreshToken(first), service.HashRefreshToken(first));
    }

    private static JwtTokenService CreateService() =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "HelpDeskPro.Tests",
            Audience = "HelpDeskPro.Tests",
            Secret = Secret,
            ExpirationMinutes = 30
        }));
}
