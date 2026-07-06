namespace HelpDeskPro.Application.Dtos.Auth;

public sealed record AuthResponse(string AccessToken, string RefreshToken, UserResponse User);
