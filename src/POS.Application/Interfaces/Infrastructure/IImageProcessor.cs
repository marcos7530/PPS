using POS.Domain.Common;

namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Port for image validation and processing (Req 16).
/// Validates format by magic bytes, checks dimensions, performs full decode,
/// and generates a 200×200 thumbnail with letterbox.
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Validates image format by magic bytes, decodes the image, checks dimensions,
    /// and generates a thumbnail. Returns validation result or error.
    /// </summary>
    Task<Result<ImageValidationResult>> ValidateAndProcessAsync(Stream imageStream, CancellationToken ct);
}

/// <summary>
/// Result of successful image validation and processing.
/// </summary>
/// <param name="ContentType">Detected MIME type (image/jpeg, image/png, image/webp).</param>
/// <param name="WidthPx">Original image width in pixels.</param>
/// <param name="HeightPx">Original image height in pixels.</param>
/// <param name="ProcessedImage">The original image bytes (re-encoded).</param>
/// <param name="Thumbnail">200×200 thumbnail bytes (letterboxed).</param>
public record ImageValidationResult(
    string ContentType,
    int WidthPx,
    int HeightPx,
    byte[] ProcessedImage,
    byte[] Thumbnail);
