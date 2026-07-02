namespace HelpDeskPro.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool EnsureCreated { get; set; } = true;
}
