namespace HelpdeskTicketManagement.Api.Requests;

public sealed class UploadAttachmentRequest
{
    public IFormFile File { get; set; } = default!;
}
