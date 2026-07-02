using HelpdeskTicketManagement.Domain.Entities;

namespace HelpdeskTicketManagement.Application.Abstractions;

public interface ITokenService
{
    string GenerateToken(AppUser user);
}
