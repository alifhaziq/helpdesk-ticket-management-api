using HelpdeskTicketManagement.Domain.Entities;

namespace HelpdeskTicketManagement.Application.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(AppUser user, string password);
    bool VerifyPassword(AppUser user, string password);
}
