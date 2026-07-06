namespace HelpDeskPro.Infrastructure.Options;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshTokens";

    public int ExpirationDays { get; set; } = 14;
}
