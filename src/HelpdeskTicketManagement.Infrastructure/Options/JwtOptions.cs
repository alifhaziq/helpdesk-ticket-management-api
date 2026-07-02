namespace HelpdeskTicketManagement.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "HelpdeskTicketManagement";
    public string Audience { get; set; } = "HelpdeskTicketManagement";
    public string Secret { get; set; } = "replace-this-development-secret-with-at-least-32-characters";
    public int ExpirationMinutes { get; set; } = 120;
}
