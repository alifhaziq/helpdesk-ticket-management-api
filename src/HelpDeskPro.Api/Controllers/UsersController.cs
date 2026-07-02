using HelpDeskPro.Application.Abstractions;
using HelpDeskPro.Application.Dtos.Auth;
using HelpDeskPro.Domain.Entities;
using HelpDeskPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public sealed class UsersController(
    IHelpDeskProDbContext dbContext,
    IPasswordHasher passwordHasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetUsers(
        [FromQuery] UserRole? role,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().Where(user => user.IsActive);

        if (role.HasValue)
        {
            query = query.Where(user => user.Role == role.Value);
        }

        var users = await query
            .OrderBy(user => user.FullName)
            .Select(user => new UserResponse(user.Id, user.FullName, user.Email, user.Role))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
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

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);
        if (exists)
        {
            return Conflict("A user with this email already exists.");
        }

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = request.Role
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new UserResponse(user.Id, user.FullName, user.Email, user.Role);
        return CreatedAtAction(nameof(GetUsers), new { role = user.Role }, response);
    }
}
