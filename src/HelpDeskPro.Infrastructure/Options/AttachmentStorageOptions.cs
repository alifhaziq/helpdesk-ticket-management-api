namespace HelpDeskPro.Infrastructure.Options;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "AttachmentStorage";

    public string RootPath { get; set; } = "attachments";
}
