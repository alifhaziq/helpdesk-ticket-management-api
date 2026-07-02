using HelpdeskTicketManagement.Domain.Enums;

namespace HelpdeskTicketManagement.Application.Dtos.Auth;

public sealed record CreateUserRequest(string FullName, string Email, string Password, UserRole Role);
