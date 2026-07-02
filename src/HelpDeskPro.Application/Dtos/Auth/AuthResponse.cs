namespace HelpDeskPro.Application.Dtos.Auth;

public sealed record AuthResponse(string AccessToken, UserResponse User);
