using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Application.Dtos.Auth;

public sealed record CreateUserRequest(string FullName, string Email, string Password, UserRole Role);
