using HelpdeskTicketManagement.Domain.Enums;

namespace HelpdeskTicketManagement.Application.Dtos.Auth;

public sealed record UserResponse(Guid Id, string FullName, string Email, UserRole Role);
