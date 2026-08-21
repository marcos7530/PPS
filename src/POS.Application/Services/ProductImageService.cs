using POS.Application.Common;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Orchestrates product image upload, replacement, and deletion (Req 16).
/// Coordinates validation, storage, persistence, and audit logging.
/// </summary>
public sealed class ProductImageService : IProductImageService
{
    private const int MinFileSize = 1;
    private const int MaxFileSize = 5_242_880;

    private readonly IProductRepository _productRepository;
    private readonly IProductImageRepository _imageRepository;
    private readonly IImageProcessor _imageProcessor;
    private readonly IImageStorage _imageStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;

    public ProductImageService(
        IProductRepository productRepository,
        IProductImageRepository imageRepository,
        IImageProcessor imageProcessor,
        IImageStorage imageStorage,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IClock clock)
    {
        _productRepository = productRepository;
        _imageRepository = imageRepository;
        _imageProcessor = imageProcessor;
        _imageStorage = imageStorage;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<Result<ProductImage>> UploadAsync(
        Guid productId, Stream imageStream, string fileName, Guid uploadedBy, CancellationToken ct)
    {
        // Validate product exists
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<ProductImage>.Failure(ErrorCode.InvalidProductIdentifier);

        // Validate file size (stream length)
        if (imageStream.Length < MinFileSize || imageStream.Length > MaxFileSize)
            return Result<ProductImage>.Failure(ErrorCode.ImageTooLarge);

        // Process and validate image (format, decode, dimensions, thumbnail)
        var processResult = await _imageProcessor.ValidateAndProcessAsync(imageStream, ct);
        if (!processResult.IsSuccess)
            return Result<ProductImage>.Failure(processResult.Error!.Value);

        var validation = processResult.Value!;

        // Check if product already has an image — if so, treat as replacement
        var existing = await _imageRepository.GetByProductIdAsync(productId, ct);
        if (existing is not null)
        {
            return await ReplaceInternalAsync(existing, validation, fileName, uploadedBy, ct);
        }

        // Save files to storage
        var storageFileName = $"{Guid.NewGuid()}{GetExtension(validation.ContentType)}";
        var thumbnailFileName = $"{Guid.NewGuid()}.png";

        string storagePath;
        string thumbnailPath;
        try
        {
            storagePath = await _imageStorage.SaveAsync(validation.ProcessedImage, storageFileName, ct);
            thumbnailPath = await _imageStorage.SaveThumbnailAsync(validation.Thumbnail, thumbnailFileName, ct);
        }
        catch
        {
            return Result<ProductImage>.Failure(ErrorCode.ImageUploadFailed);
        }

        // Create entity
        var now = _clock.UtcNow;
        var imageEntity = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OriginalFileName = fileName,
            ContentType = validation.ContentType,
            ByteSize = validation.ProcessedImage.Length,
            WidthPx = validation.WidthPx,
            HeightPx = validation.HeightPx,
            StoragePath = storagePath,
            ThumbnailPath = thumbnailPath,
            UploadedBy = uploadedBy,
            UploadedAt = now
        };

        await _imageRepository.AddAsync(imageEntity, ct);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "UploadProductImage",
            EntityType: "ProductImage",
            EntityId: imageEntity.Id,
            RelatedEntityIds: new List<Guid> { productId },
            BeforeState: null,
            AfterState: $"{{\"fileName\":\"{fileName}\",\"contentType\":\"{validation.ContentType}\",\"byteSize\":{validation.ProcessedImage.Length},\"width\":{validation.WidthPx},\"height\":{validation.HeightPx}}}",
            Metadata: $"{{\"uploadedBy\":\"{uploadedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            // Attempt cleanup of stored files on failure
            try
            {
                await _imageStorage.DeleteAsync(storagePath, ct);
                await _imageStorage.DeleteAsync(thumbnailPath, ct);
            }
            catch
            {
                // Best effort cleanup
            }
            return Result<ProductImage>.Failure(ErrorCode.ImageUploadFailed);
        }

        return Result<ProductImage>.Success(imageEntity);
    }

    /// <inheritdoc />
    public async Task<Result<ProductImage>> ReplaceAsync(
        Guid productId, Stream imageStream, string fileName, Guid uploadedBy, CancellationToken ct)
    {
        // Validate product exists
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<ProductImage>.Failure(ErrorCode.InvalidProductIdentifier);

        // Validate file size
        if (imageStream.Length < MinFileSize || imageStream.Length > MaxFileSize)
            return Result<ProductImage>.Failure(ErrorCode.ImageTooLarge);

        // Process and validate image
        var processResult = await _imageProcessor.ValidateAndProcessAsync(imageStream, ct);
        if (!processResult.IsSuccess)
            return Result<ProductImage>.Failure(processResult.Error!.Value);

        var validation = processResult.Value!;

        // Get existing image
        var existing = await _imageRepository.GetByProductIdAsync(productId, ct);
        if (existing is null)
        {
            // No existing image — treat as new upload
            return await SaveNewImageAsync(productId, validation, fileName, uploadedBy, "ReplaceProductImage", ct);
        }

        return await ReplaceInternalAsync(existing, validation, fileName, uploadedBy, ct);
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteAsync(Guid productId, Guid deletedBy, CancellationToken ct)
    {
        // Validate product exists
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Unit>.Failure(ErrorCode.InvalidProductIdentifier);

        var existing = await _imageRepository.GetByProductIdAsync(productId, ct);
        if (existing is null)
            return Result<Unit>.Success(Unit.Value);

        var oldStoragePath = existing.StoragePath;
        var oldThumbnailPath = existing.ThumbnailPath;

        _imageRepository.Remove(existing);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "DeleteProductImage",
            EntityType: "ProductImage",
            EntityId: existing.Id,
            RelatedEntityIds: new List<Guid> { productId },
            BeforeState: $"{{\"fileName\":\"{existing.OriginalFileName}\",\"contentType\":\"{existing.ContentType}\",\"byteSize\":{existing.ByteSize},\"width\":{existing.WidthPx},\"height\":{existing.HeightPx}}}",
            AfterState: null,
            Metadata: $"{{\"deletedBy\":\"{deletedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<Unit>.Failure(ErrorCode.ImageUploadFailed);
        }

        // Delete files after successful DB commit
        try
        {
            await _imageStorage.DeleteAsync(oldStoragePath, ct);
            await _imageStorage.DeleteAsync(oldThumbnailPath, ct);
        }
        catch
        {
            // Best effort — files are orphaned but DB is consistent
        }

        return Result<Unit>.Success(Unit.Value);
    }

    // --- Private helpers ---

    private async Task<Result<ProductImage>> ReplaceInternalAsync(
        ProductImage existing, ImageValidationResult validation, string fileName, Guid uploadedBy, CancellationToken ct)
    {
        var oldStoragePath = existing.StoragePath;
        var oldThumbnailPath = existing.ThumbnailPath;
        var beforeState = $"{{\"fileName\":\"{existing.OriginalFileName}\",\"contentType\":\"{existing.ContentType}\",\"byteSize\":{existing.ByteSize},\"width\":{existing.WidthPx},\"height\":{existing.HeightPx}}}";

        // Save new files
        var storageFileName = $"{Guid.NewGuid()}{GetExtension(validation.ContentType)}";
        var thumbnailFileName = $"{Guid.NewGuid()}.png";

        string storagePath;
        string thumbnailPath;
        try
        {
            storagePath = await _imageStorage.SaveAsync(validation.ProcessedImage, storageFileName, ct);
            thumbnailPath = await _imageStorage.SaveThumbnailAsync(validation.Thumbnail, thumbnailFileName, ct);
        }
        catch
        {
            return Result<ProductImage>.Failure(ErrorCode.ImageUploadFailed);
        }

        // Update entity
        var now = _clock.UtcNow;
        existing.OriginalFileName = fileName;
        existing.ContentType = validation.ContentType;
        existing.ByteSize = validation.ProcessedImage.Length;
        existing.WidthPx = validation.WidthPx;
        existing.HeightPx = validation.HeightPx;
        existing.StoragePath = storagePath;
        existing.ThumbnailPath = thumbnailPath;
        existing.UploadedBy = uploadedBy;
        existing.UploadedAt = now;

        _imageRepository.Update(existing);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "ReplaceProductImage",
            EntityType: "ProductImage",
            EntityId: existing.Id,
            RelatedEntityIds: new List<Guid> { existing.ProductId },
            BeforeState: beforeState,
            AfterState: $"{{\"fileName\":\"{fileName}\",\"contentType\":\"{validation.ContentType}\",\"byteSize\":{validation.ProcessedImage.Length},\"width\":{validation.WidthPx},\"height\":{validation.HeightPx}}}",
            Metadata: $"{{\"uploadedBy\":\"{uploadedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            // Cleanup new files
            try
            {
                await _imageStorage.DeleteAsync(storagePath, ct);
                await _imageStorage.DeleteAsync(thumbnailPath, ct);
            }
            catch { /* best effort */ }
            return Result<ProductImage>.Failure(ErrorCode.ImageUploadFailed);
        }

        // Delete old files after successful commit
        try
        {
            await _imageStorage.DeleteAsync(oldStoragePath, ct);
            await _imageStorage.DeleteAsync(oldThumbnailPath, ct);
        }
        catch
        {
            // Best effort — old files orphaned but DB is consistent
        }

        return Result<ProductImage>.Success(existing);
    }

    private async Task<Result<ProductImage>> SaveNewImageAsync(
        Guid productId, ImageValidationResult validation, string fileName, Guid uploadedBy,
        string operationType, CancellationToken ct)
    {
        var storageFileName = $"{Guid.NewGuid()}{GetExtension(validation.ContentType)}";
        var thumbnailFileName = $"{Guid.NewGuid()}.png";

        string storagePath;
        string thumbnailPath;
        try
        {
            storagePath = await _imageStorage.SaveAsync(validation.ProcessedImage, storageFileName, ct);
            thumbnailPath = await _imageStorage.SaveThumbnailAsync(validation.Thumbnail, thumbnailFileName, ct);
        }
        catch
        {
            return Result<ProductImage>.Failure(ErrorCode.ImageUploadFailed);
        }

        var now = _clock.UtcNow;
        var imageEntity = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            OriginalFileName = fileName,
            ContentType = validation.ContentType,
            ByteSize = validation.ProcessedImage.Length,
            WidthPx = validation.WidthPx,
            HeightPx = validation.HeightPx,
            StoragePath = storagePath,
            ThumbnailPath = thumbnailPath,
            UploadedBy = uploadedBy,
            UploadedAt = now
        };

        await _imageRepository.AddAsync(imageEntity, ct);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: operationType,
            EntityType: "ProductImage",
            EntityId: imageEntity.Id,
            RelatedEntityIds: new List<Guid> { productId },
            BeforeState: null,
            AfterState: $"{{\"fileName\":\"{fileName}\",\"contentType\":\"{validation.ContentType}\",\"byteSize\":{validation.ProcessedImage.Length},\"width\":{validation.WidthPx},\"height\":{validation.HeightPx}}}",
            Metadata: $"{{\"uploadedBy\":\"{uploadedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            try
            {
                await _imageStorage.DeleteAsync(storagePath, ct);
                await _imageStorage.DeleteAsync(thumbnailPath, ct);
            }
            catch { /* best effort */ }
            return Result<ProductImage>.Failure(ErrorCode.ImageUploadFailed);
        }

        return Result<ProductImage>.Success(imageEntity);
    }

    private static string GetExtension(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}
