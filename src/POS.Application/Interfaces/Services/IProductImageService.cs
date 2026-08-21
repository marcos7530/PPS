using POS.Application.Common;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for product image management (Req 16).
/// </summary>
public interface IProductImageService
{
    /// <summary>
    /// Uploads and validates a product image (format, size, dimensions).
    /// Generates thumbnail. Replaces existing image if present.
    /// </summary>
    Task<Result<ProductImage>> UploadAsync(Guid productId, Stream imageStream, string fileName, Guid uploadedBy, CancellationToken ct);

    /// <summary>
    /// Replaces the existing product image with a new one.
    /// </summary>
    Task<Result<ProductImage>> ReplaceAsync(Guid productId, Stream imageStream, string fileName, Guid uploadedBy, CancellationToken ct);

    /// <summary>
    /// Deletes the product image and its thumbnail from storage.
    /// </summary>
    Task<Result<Unit>> DeleteAsync(Guid productId, Guid deletedBy, CancellationToken ct);
}
