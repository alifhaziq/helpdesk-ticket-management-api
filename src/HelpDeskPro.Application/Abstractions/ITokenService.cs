using HelpDeskPro.Domain.Entities;

namespace HelpDeskPro.Application.Abstractions;

public interface ITokenService
{
    string GenerateAccessToken(AppUser user);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
