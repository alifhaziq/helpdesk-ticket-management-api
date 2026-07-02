using HelpDeskPro.Domain.Entities;

namespace HelpDeskPro.Application.Abstractions;

public interface ITokenService
{
    string GenerateToken(AppUser user);
}
