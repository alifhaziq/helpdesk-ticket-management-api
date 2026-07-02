namespace HelpDeskPro.Application.Abstractions;

public sealed record FileUpload(string FileName, string ContentType, long Length, Stream Content);

public sealed record StoredFile(string FileName, string StoragePath);

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(FileUpload file, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);
}
