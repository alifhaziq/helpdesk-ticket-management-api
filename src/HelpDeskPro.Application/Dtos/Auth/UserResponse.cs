using HelpDeskPro.Domain.Enums;

namespace HelpDeskPro.Application.Dtos.Auth;

public sealed record UserResponse(Guid Id, string FullName, string Email, UserRole Role);
