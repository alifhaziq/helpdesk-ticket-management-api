namespace HelpDeskPro.Infrastructure.Options;

public sealed class AdminSeedOptions
{
    public const string SectionName = "SeedAdmin";

    public bool Enabled { get; set; } = true;
    public string FullName { get; set; } = "System Administrator";
    public string Email { get; set; } = "admin@helpdesk.local";
    public string Password { get; set; } = "Admin123!";
}
