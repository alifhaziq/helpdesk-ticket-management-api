namespace HelpDeskPro.Infrastructure.Options;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "no-reply@helpdesk.local";
    public string FromName { get; set; } = "HelpDesk Pro";
}
