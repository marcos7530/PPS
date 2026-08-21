namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Port for image file persistence (Req 16).
/// Handles saving and deleting original images and thumbnails.
/// </summary>
public interface IImageStorage
{
    /// <summary>
    /// Saves the image data and returns the storage path.
    /// </summary>
    Task<string> SaveAsync(byte[] data, string fileName, CancellationToken ct);

    /// <summary>
    /// Saves the thumbnail data and returns the storage path.
    /// </summary>
    Task<string> SaveThumbnailAsync(byte[] data, string fileName, CancellationToken ct);

    /// <summary>
    /// Deletes a file at the given storage path.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken ct);
}
