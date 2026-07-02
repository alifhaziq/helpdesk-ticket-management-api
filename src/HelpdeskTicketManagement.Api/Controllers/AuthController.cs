using HelpdeskTicketManagement.Application.Abstractions;
using HelpdeskTicketManagement.Application.Dtos.Auth;
using HelpdeskTicketManagement.Domain.Entities;
using HelpdeskTicketManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskTicketManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    IHelpdeskDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUserService currentUser) : ControllerBase
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
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(CreateAuthResponse(user));
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

        return Ok(CreateAuthResponse(user));
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

    private AuthResponse CreateAuthResponse(AppUser user) =>
        new(tokenService.GenerateToken(user), ToUserResponse(user));

    private static UserResponse ToUserResponse(AppUser user) =>
        new(user.Id, user.FullName, user.Email, user.Role);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
