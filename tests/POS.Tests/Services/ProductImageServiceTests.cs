using POS.Application.Common;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Application.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Tests.Services;

/// <summary>
/// Unit tests for ProductImageService (Req 16.1–16.15, 16.23–16.25).
/// </summary>
public class ProductImageServiceTests : IDisposable
{
    private readonly FakeProductRepositoryForImages _productRepo = new();
    private readonly FakeProductImageRepository _imageRepo = new();
    private readonly FakeImageProcessor _imageProcessor = new();
    private readonly FakeImageStorage _imageStorage = new();
    private readonly FakeUnitOfWorkForImages _unitOfWork = new();
    private readonly FakeAuditWriterForImages _auditWriter = new();
    private readonly FakeClockForImages _clock = new();
    private readonly ProductImageService _sut;

    private static readonly Guid TestProductId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    public ProductImageServiceTests()
    {
        _sut = new ProductImageService(
            _productRepo,
            _imageRepo,
            _imageProcessor,
            _imageStorage,
            _unitOfWork,
            _auditWriter,
            _clock);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- UploadAsync Tests ---

    [Fact]
    public async Task Upload_ValidImage_ReturnsSuccess()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.UploadAsync(TestProductId, stream, "photo.jpg", TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestProductId, result.Value!.ProductId);
        Assert.Equal("photo.jpg", result.Value.OriginalFileName);
        Assert.Equal("image/jpeg", result.Value.ContentType);
        Assert.Equal(TestUserId, result.Value.UploadedBy);
    }

    [Fact]
    public async Task Upload_ValidImage_PersistsEntity()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(1024);
        await _sut.UploadAsync(TestProductId, stream, "photo.jpg", TestUserId, CancellationToken.None);

        Assert.Single(_imageRepo.Images);
    }

    [Fact]
    public async Task Upload_ValidImage_SavesFilesToStorage()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(1024);
        await _sut.UploadAsync(TestProductId, stream, "photo.jpg", TestUserId, CancellationToken.None);

        Assert.Equal(1, _imageStorage.SavedOriginals);
        Assert.Equal(1, _imageStorage.SavedThumbnails);
    }

    [Fact]
    public async Task Upload_ValidImage_EnqueuesAuditEntry()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(1024);
        await _sut.UploadAsync(TestProductId, stream, "photo.jpg", TestUserId, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("UploadProductImage", _auditWriter.EnqueuedDrafts[0].OperationType);
        Assert.Equal("ProductImage", _auditWriter.EnqueuedDrafts[0].EntityType);
    }

    [Fact]
    public async Task Upload_NonExistentProduct_ReturnsInvalidProductIdentifier()
    {
        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.UploadAsync(Guid.NewGuid(), stream, "photo.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Upload_StreamTooLarge_ReturnsImageTooLarge()
    {
        _productRepo.Products.Add(CreateProduct());

        using var stream = CreateStreamOfSize(5_242_881);
        var result = await _sut.UploadAsync(TestProductId, stream, "huge.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageTooLarge, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Upload_EmptyStream_ReturnsImageTooLarge()
    {
        _productRepo.Products.Add(CreateProduct());

        using var stream = CreateStreamOfSize(0);
        var result = await _sut.UploadAsync(TestProductId, stream, "empty.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageTooLarge, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Upload_UnsupportedFormat_ReturnsUnsupportedImageFormat()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.NextResult = Result<ImageValidationResult>.Failure(ErrorCode.UnsupportedImageFormat);

        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.UploadAsync(TestProductId, stream, "file.bmp", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.UnsupportedImageFormat, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Upload_CorruptedImage_ReturnsImageCorrupted()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.NextResult = Result<ImageValidationResult>.Failure(ErrorCode.ImageCorrupted);

        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.UploadAsync(TestProductId, stream, "corrupt.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageCorrupted, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Upload_DimensionsExceeded_ReturnsImageDimensionsExceeded()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.NextResult = Result<ImageValidationResult>.Failure(ErrorCode.ImageDimensionsExceeded);

        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.UploadAsync(TestProductId, stream, "big.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageDimensionsExceeded, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Upload_StorageFails_ReturnsImageUploadFailed()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();
        _imageStorage.ShouldFail = true;

        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.UploadAsync(TestProductId, stream, "photo.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageUploadFailed, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Upload_ProductAlreadyHasImage_PerformsReplacement()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.UploadAsync(TestProductId, stream, "new_photo.jpg", TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new_photo.jpg", result.Value!.OriginalFileName);
        // Should have replaced — audit should show ReplaceProductImage
        Assert.Contains(_auditWriter.EnqueuedDrafts, d => d.OperationType == "ReplaceProductImage");
    }

    [Fact]
    public async Task Upload_ProductAlreadyHasImage_DeletesOldFiles()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(1024);
        await _sut.UploadAsync(TestProductId, stream, "new_photo.jpg", TestUserId, CancellationToken.None);

        // Old files should have been deleted (old storage + old thumbnail)
        Assert.Equal(2, _imageStorage.DeletedPaths.Count);
    }

    [Fact]
    public async Task Upload_ExactMaxSize_ReturnsSuccess()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(5_242_880);
        var result = await _sut.UploadAsync(TestProductId, stream, "max.jpg", TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Upload_ExactMinSize_ReturnsSuccess()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(1);
        var result = await _sut.UploadAsync(TestProductId, stream, "tiny.jpg", TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- ReplaceAsync Tests ---

    [Fact]
    public async Task Replace_ExistingImage_ReturnsSuccess()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(2048);
        var result = await _sut.ReplaceAsync(TestProductId, stream, "replaced.png", TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("replaced.png", result.Value!.OriginalFileName);
    }

    [Fact]
    public async Task Replace_ExistingImage_DeletesOldFiles()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(2048);
        await _sut.ReplaceAsync(TestProductId, stream, "replaced.png", TestUserId, CancellationToken.None);

        Assert.Equal(2, _imageStorage.DeletedPaths.Count);
        Assert.Contains("originals/old_storage.jpg", _imageStorage.DeletedPaths);
        Assert.Contains("thumbnails/old_thumb.png", _imageStorage.DeletedPaths);
    }

    [Fact]
    public async Task Replace_ExistingImage_EnqueuesReplaceAuditEntry()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(2048);
        await _sut.ReplaceAsync(TestProductId, stream, "replaced.png", TestUserId, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("ReplaceProductImage", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    [Fact]
    public async Task Replace_NoExistingImage_CreatesNew()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageProcessor.SetValidResult();

        using var stream = CreateStreamOfSize(2048);
        var result = await _sut.ReplaceAsync(TestProductId, stream, "new.png", TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_imageRepo.Images);
    }

    [Fact]
    public async Task Replace_NonExistentProduct_ReturnsInvalidProductIdentifier()
    {
        using var stream = CreateStreamOfSize(1024);
        var result = await _sut.ReplaceAsync(Guid.NewGuid(), stream, "photo.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Replace_ImageTooLarge_ReturnsImageTooLarge()
    {
        _productRepo.Products.Add(CreateProduct());

        using var stream = CreateStreamOfSize(5_242_881);
        var result = await _sut.ReplaceAsync(TestProductId, stream, "huge.jpg", TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ImageTooLarge, result.Error!.Value.Code);
    }

    // --- DeleteAsync Tests ---

    [Fact]
    public async Task Delete_ExistingImage_ReturnsSuccess()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());

        var result = await _sut.DeleteAsync(TestProductId, TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Delete_ExistingImage_RemovesEntity()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());

        await _sut.DeleteAsync(TestProductId, TestUserId, CancellationToken.None);

        Assert.Empty(_imageRepo.Images);
    }

    [Fact]
    public async Task Delete_ExistingImage_DeletesBothFiles()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());

        await _sut.DeleteAsync(TestProductId, TestUserId, CancellationToken.None);

        Assert.Equal(2, _imageStorage.DeletedPaths.Count);
        Assert.Contains("originals/old_storage.jpg", _imageStorage.DeletedPaths);
        Assert.Contains("thumbnails/old_thumb.png", _imageStorage.DeletedPaths);
    }

    [Fact]
    public async Task Delete_ExistingImage_EnqueuesDeleteAuditEntry()
    {
        _productRepo.Products.Add(CreateProduct());
        _imageRepo.Images.Add(CreateExistingImage());

        await _sut.DeleteAsync(TestProductId, TestUserId, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("DeleteProductImage", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    [Fact]
    public async Task Delete_NoExistingImage_ReturnsSuccessWithNoOp()
    {
        _productRepo.Products.Add(CreateProduct());

        var result = await _sut.DeleteAsync(TestProductId, TestUserId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_imageStorage.DeletedPaths);
    }

    [Fact]
    public async Task Delete_NonExistentProduct_ReturnsInvalidProductIdentifier()
    {
        var result = await _sut.DeleteAsync(Guid.NewGuid(), TestUserId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    // --- Helpers ---

    private static Product CreateProduct() => new()
    {
        Id = TestProductId,
        Name = "Test Product",
        Sku = "SKU-IMG-001",
        CategoryId = Guid.NewGuid(),
        SalePrice = 10.00m,
        CostPrice = 5.00m,
        Quantity = 100,
        MinStockThreshold = 10,
        IsDeactivated = false,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        RowVersion = Array.Empty<byte>()
    };

    private static ProductImage CreateExistingImage() => new()
    {
        Id = Guid.NewGuid(),
        ProductId = TestProductId,
        OriginalFileName = "old_photo.jpg",
        ContentType = "image/jpeg",
        ByteSize = 500,
        WidthPx = 800,
        HeightPx = 600,
        StoragePath = "originals/old_storage.jpg",
        ThumbnailPath = "thumbnails/old_thumb.png",
        UploadedBy = Guid.NewGuid(),
        UploadedAt = DateTimeOffset.UtcNow.AddDays(-1)
    };

    private static MemoryStream CreateStreamOfSize(int size)
    {
        var data = new byte[size];
        return new MemoryStream(data);
    }
}

// --- Fakes for ProductImageService tests ---

internal sealed class FakeProductRepositoryForImages : IProductRepository
{
    public List<Product> Products { get; } = new();

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Products.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Product>>(Products.AsReadOnly());

    public Task AddAsync(Product entity, CancellationToken ct = default)
    {
        Products.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Product entity) { }
    public void Remove(Product entity) => Products.Remove(entity);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
        => Task.FromResult(Products.FirstOrDefault(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)));

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult<Product?>(null);

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default)
        => Task.FromResult(Products.Any(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<IReadOnlyList<Product>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Product>>(new List<Product>().AsReadOnly());

    public Task<IReadOnlyList<Product>> SearchByNameAsync(string term, int maxResults, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Product>>(new List<Product>().AsReadOnly());
}

internal sealed class FakeProductImageRepository : IProductImageRepository
{
    public List<ProductImage> Images { get; } = new();

    public Task<ProductImage?> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => Task.FromResult(Images.FirstOrDefault(i => i.ProductId == productId));

    public Task<ProductImage?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Images.FirstOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<ProductImage>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductImage>>(Images.AsReadOnly());

    public Task AddAsync(ProductImage entity, CancellationToken ct = default)
    {
        Images.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(ProductImage entity) { }
    public void Remove(ProductImage entity) => Images.Remove(entity);
}

internal sealed class FakeImageProcessor : IImageProcessor
{
    public Result<ImageValidationResult>? NextResult { get; set; }

    public void SetValidResult()
    {
        NextResult = Result<ImageValidationResult>.Success(new ImageValidationResult(
            ContentType: "image/jpeg",
            WidthPx: 800,
            HeightPx: 600,
            ProcessedImage: new byte[500],
            Thumbnail: new byte[100]));
    }

    public Task<Result<ImageValidationResult>> ValidateAndProcessAsync(Stream imageStream, CancellationToken ct)
    {
        return Task.FromResult(NextResult ?? Result<ImageValidationResult>.Failure(ErrorCode.ImageCorrupted));
    }
}

internal sealed class FakeImageStorage : IImageStorage
{
    public int SavedOriginals { get; private set; }
    public int SavedThumbnails { get; private set; }
    public List<string> DeletedPaths { get; } = new();
    public bool ShouldFail { get; set; }

    public Task<string> SaveAsync(byte[] data, string fileName, CancellationToken ct)
    {
        if (ShouldFail) throw new IOException("Storage failed");
        SavedOriginals++;
        return Task.FromResult($"originals/{fileName}");
    }

    public Task<string> SaveThumbnailAsync(byte[] data, string fileName, CancellationToken ct)
    {
        if (ShouldFail) throw new IOException("Storage failed");
        SavedThumbnails++;
        return Task.FromResult($"thumbnails/{fileName}");
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        DeletedPaths.Add(path);
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWorkForImages : IUnitOfWork
{
    public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeAuditWriterForImages : IAuditWriter
{
    public List<AuditEntryDraft> EnqueuedDrafts { get; } = new();
    public List<(ErrorCode code, AuditContext ctx)> FailedAttempts { get; } = new();

    public void Enqueue(AuditEntryDraft draft) => EnqueuedDrafts.Add(draft);

    public Task WriteFailedAttemptAsync(ErrorCode code, AuditContext ctx, CancellationToken ct)
    {
        FailedAttempts.Add((code, ctx));
        return Task.CompletedTask;
    }
}

internal sealed class FakeClockForImages : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2024, 7, 1, 12, 0, 0, TimeSpan.Zero);
}
