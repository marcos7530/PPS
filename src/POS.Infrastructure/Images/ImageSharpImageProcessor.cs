using POS.Application.Interfaces.Infrastructure;
using POS.Domain.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace POS.Infrastructure.Images;

/// <summary>
/// ImageSharp-based implementation of IImageProcessor (Req 16.5, 16.9, 16.10).
/// Validates format by magic bytes, checks dimensions, performs full decode,
/// and generates 200×200 thumbnail with letterbox.
/// </summary>
public sealed class ImageSharpImageProcessor : IImageProcessor
{
    private const int MaxDimensionPx = 4000;
    private const int ThumbnailSize = 200;

    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public async Task<Result<ImageValidationResult>> ValidateAndProcessAsync(Stream imageStream, CancellationToken ct)
    {
        // Detect format by magic bytes (not extension)
        IImageFormat? detectedFormat;
        try
        {
            detectedFormat = await Image.DetectFormatAsync(imageStream, ct);
        }
        catch
        {
            return Result<ImageValidationResult>.Failure(ErrorCode.UnsupportedImageFormat);
        }

        if (detectedFormat is null)
            return Result<ImageValidationResult>.Failure(ErrorCode.UnsupportedImageFormat);

        var mimeType = detectedFormat.DefaultMimeType;
        if (!SupportedMimeTypes.Contains(mimeType))
            return Result<ImageValidationResult>.Failure(ErrorCode.UnsupportedImageFormat);

        // Reset stream for full decode
        imageStream.Position = 0;

        // Full image decode validation
        Image<Rgba32> image;
        try
        {
            image = await Image.LoadAsync<Rgba32>(imageStream, ct);
        }
        catch
        {
            return Result<ImageValidationResult>.Failure(ErrorCode.ImageCorrupted);
        }

        using (image)
        {
            // Validate dimensions
            if (image.Width > MaxDimensionPx || image.Height > MaxDimensionPx)
                return Result<ImageValidationResult>.Failure(ErrorCode.ImageDimensionsExceeded);

            var widthPx = image.Width;
            var heightPx = image.Height;

            // Re-encode original image to ensure clean data
            byte[] processedImage;
            using (var ms = new MemoryStream())
            {
                var encoder = GetEncoder(mimeType);
                await image.SaveAsync(ms, encoder, ct);
                processedImage = ms.ToArray();
            }

            // Generate 200×200 thumbnail with letterbox (preserve aspect ratio, pad remaining space)
            byte[] thumbnail;
            using (var thumbImage = image.Clone(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(ThumbnailSize, ThumbnailSize),
                    Mode = ResizeMode.Pad,
                    PadColor = Color.White
                });
            }))
            {
                using var ms = new MemoryStream();
                // Thumbnails are always PNG for quality and transparency support
                await thumbImage.SaveAsync(ms, new PngEncoder(), ct);
                thumbnail = ms.ToArray();
            }

            return Result<ImageValidationResult>.Success(new ImageValidationResult(
                ContentType: mimeType,
                WidthPx: widthPx,
                HeightPx: heightPx,
                ProcessedImage: processedImage,
                Thumbnail: thumbnail));
        }
    }

    private static IImageEncoder GetEncoder(string mimeType) => mimeType.ToLowerInvariant() switch
    {
        "image/jpeg" => new JpegEncoder { Quality = 85 },
        "image/png" => new PngEncoder(),
        "image/webp" => new WebpEncoder { Quality = 85 },
        _ => new PngEncoder()
    };
}
