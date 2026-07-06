using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Application.Dtos.Auth;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using HelpDeskPro.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HelpDeskPro.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    IHelpDeskProDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUserService currentUser,
    IAuditService auditService,
    IOptions<RefreshTokenOptions> refreshTokenOptions) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Full name, email, and password are required.");
        }

        if (request.Password.Length < 8)
        {
            return BadRequest("Password must be at least 8 characters.");
        }

        var email = NormalizeEmail(request.Email);
        var exists = await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);
        if (exists)
        {
            return Conflict("A user with this email already exists.");
        }

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = UserRole.User
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        var refreshToken = AddRefreshToken(user);
        await auditService.RecordAsync(
            "Auth.Register",
            nameof(AppUser),
            user.Id,
            new { user.Email, user.Role },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(CreateAuthResponse(user, refreshToken.Token));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required.");
        }

        var email = NormalizeEmail(request.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Email == email && candidate.IsActive,
            cancellationToken);

        if (user is null || !passwordHasher.VerifyPassword(user, request.Password))
        {
            return Unauthorized("Invalid email or password.");
        }

        var refreshToken = AddRefreshToken(user);
        await auditService.RecordAsync(
            "Auth.Login",
            nameof(AppUser),
            user.Id,
            new { user.Email, user.Role },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(CreateAuthResponse(user, refreshToken.Token));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token is required.");
        }

        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null ||
            storedToken.RevokedAt is not null ||
            storedToken.ExpiresAt <= DateTimeOffset.UtcNow ||
            storedToken.User is null ||
            !storedToken.User.IsActive)
        {
            return Unauthorized("Invalid refresh token.");
        }

        var replacementToken = AddRefreshToken(storedToken.User);
        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.ReplacedByTokenId = replacementToken.Entity.Id;

        await auditService.RecordAsync(
            "Auth.Refresh",
            nameof(RefreshToken),
            storedToken.Id,
            new { storedToken.UserId, ReplacementTokenId = replacementToken.Entity.Id },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(CreateAuthResponse(storedToken.User, replacementToken.Token));
    }

    [AllowAnonymous]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(
        RevokeRefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest("Refresh token is required.");
        }

        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAt is not null)
        {
            return NoContent();
        }

        storedToken.RevokedAt = DateTimeOffset.UtcNow;

        await auditService.RecordAsync(
            "Auth.RevokeRefreshToken",
            nameof(RefreshToken),
            storedToken.Id,
            new { storedToken.UserId },
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive, cancellationToken);

        return user is null ? Unauthorized() : Ok(ToUserResponse(user));
    }

    private AuthResponse CreateAuthResponse(AppUser user, string refreshToken) =>
        new(tokenService.GenerateAccessToken(user), refreshToken, ToUserResponse(user));

    private (string Token, RefreshToken Entity) AddRefreshToken(AppUser user)
    {
        var token = tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(token),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, refreshTokenOptions.Value.ExpirationDays))
        };

        dbContext.RefreshTokens.Add(refreshToken);
        return (token, refreshToken);
    }

    private static UserResponse ToUserResponse(AppUser user) =>
        new(user.Id, user.FullName, user.Email, user.Role);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
