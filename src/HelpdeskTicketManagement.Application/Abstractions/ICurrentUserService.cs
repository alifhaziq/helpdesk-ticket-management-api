using HelpdeskTicketManagement.Domain.Enums;

namespace HelpdeskTicketManagement.Application.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
