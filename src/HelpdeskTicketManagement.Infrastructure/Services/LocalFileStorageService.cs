using HelpdeskTicketManagement.Application.Abstractions;
using HelpdeskTicketManagement.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace HelpdeskTicketManagement.Infrastructure.Services;

public sealed class LocalFileStorageService(IOptions<AttachmentStorageOptions> options) : IFileStorageService
{
    private readonly string _rootPath = ResolveRootPath(options.Value.RootPath);

    public async Task<StoredFile> SaveAsync(FileUpload file, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        Directory.CreateDirectory(_rootPath);

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var dateSegment = DateTime.UtcNow.ToString("yyyyMMdd");
        var relativePath = Path.Combine(dateSegment, storedFileName);
        var absoluteDirectory = Path.Combine(_rootPath, dateSegment);
        var absolutePath = Path.Combine(absoluteDirectory, storedFileName);

        Directory.CreateDirectory(absoluteDirectory);

        await using var target = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await file.Content.CopyToAsync(target, cancellationToken);

        return new StoredFile(storedFileName, relativePath.Replace('\\', '/'));
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = GetSafeAbsolutePath(storagePath);
        if (absolutePath is null || !File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    private static string ResolveRootPath(string configuredRoot)
    {
        var root = string.IsNullOrWhiteSpace(configuredRoot) ? "attachments" : configuredRoot;
        var absoluteRoot = Path.IsPathRooted(root)
            ? root
            : Path.Combine(AppContext.BaseDirectory, root);

        return Path.GetFullPath(absoluteRoot);
    }

    private string? GetSafeAbsolutePath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        var normalizedRelativePath = storagePath.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(_rootPath, normalizedRelativePath));

        return candidate.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }
}
