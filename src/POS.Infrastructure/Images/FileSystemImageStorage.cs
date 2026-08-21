using POS.Application.Interfaces.Infrastructure;

namespace POS.Infrastructure.Images;

/// <summary>
/// File-system-based implementation of IImageStorage (Req 16).
/// Stores images in a configured base directory with separate subdirectories
/// for originals and thumbnails.
/// </summary>
public sealed class FileSystemImageStorage : IImageStorage
{
    private readonly string _basePath;
    private const string OriginalsFolder = "originals";
    private const string ThumbnailsFolder = "thumbnails";

    public FileSystemImageStorage(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(Path.Combine(_basePath, OriginalsFolder));
        Directory.CreateDirectory(Path.Combine(_basePath, ThumbnailsFolder));
    }

    public async Task<string> SaveAsync(byte[] data, string fileName, CancellationToken ct)
    {
        var relativePath = Path.Combine(OriginalsFolder, fileName);
        var fullPath = Path.Combine(_basePath, relativePath);

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(fullPath, data, ct);
        return relativePath;
    }

    public async Task<string> SaveThumbnailAsync(byte[] data, string fileName, CancellationToken ct)
    {
        var relativePath = Path.Combine(ThumbnailsFolder, fileName);
        var fullPath = Path.Combine(_basePath, relativePath);

        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(fullPath, data, ct);
        return relativePath;
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var fullPath = Path.Combine(_basePath, path);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }
}
