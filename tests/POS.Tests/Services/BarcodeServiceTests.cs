using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Tests.Services;

/// <summary>
/// Unit tests for BarcodeService (Req 18.1–18.5, 18.17–18.19).
/// </summary>
public class BarcodeServiceTests : IDisposable
{
    private readonly FakeProductRepositoryForBarcode _productRepo = new();
    private readonly FakeClockForBarcode _clock = new();
    private readonly FakeUnitOfWorkForBarcode _unitOfWork = new();
    private readonly FakeAuditWriterForBarcode _auditWriter = new();
    private readonly BarcodeService _sut;

    public BarcodeServiceTests()
    {
        _sut = new BarcodeService(
            _productRepo,
            _clock,
            _unitOfWork,
            _auditWriter);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- AssignBarcodeAsync Tests (Req 18.1–18.4) ---

    [Fact]
    public async Task AssignBarcode_ValidEan13_ReturnsSuccess()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Valid EAN-13: 4006381333931
        var result = await _sut.AssignBarcodeAsync(
            product.Id, "4006381333931", BarcodeFormat.Ean13, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("4006381333931", result.Value!.BarcodeValue);
        Assert.Equal("Ean13", result.Value.BarcodeFormat);
    }

    [Fact]
    public async Task AssignBarcode_ValidUpcA_ReturnsSuccess()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Valid UPC-A: 012345678905
        var result = await _sut.AssignBarcodeAsync(
            product.Id, "012345678905", BarcodeFormat.UpcA, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("012345678905", result.Value!.BarcodeValue);
        Assert.Equal("UpcA", result.Value.BarcodeFormat);
    }

    [Fact]
    public async Task AssignBarcode_ValidCode128_ReturnsSuccess()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        var result = await _sut.AssignBarcodeAsync(
            product.Id, "ABC123", BarcodeFormat.Code128, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ABC123", result.Value!.BarcodeValue);
        Assert.Equal("Code128", result.Value.BarcodeFormat);
    }

    [Fact]
    public async Task AssignBarcode_InvalidEan13Format_ReturnsFailure()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Invalid EAN-13: wrong length
        var result = await _sut.AssignBarcodeAsync(
            product.Id, "12345", BarcodeFormat.Ean13, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidBarcodeFormat, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_InvalidEan13CheckDigit_ReturnsFailure()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Invalid EAN-13: correct length but wrong check digit
        var result = await _sut.AssignBarcodeAsync(
            product.Id, "4006381333932", BarcodeFormat.Ean13, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidBarcodeCheckDigit, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_InvalidUpcACheckDigit_ReturnsFailure()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Invalid UPC-A: correct length but wrong check digit
        var result = await _sut.AssignBarcodeAsync(
            product.Id, "012345678901", BarcodeFormat.UpcA, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidBarcodeCheckDigit, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_Code128TooLong_ReturnsFailure()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Code 128 max is 48 chars
        var longBarcode = new string('A', 49);
        var result = await _sut.AssignBarcodeAsync(
            product.Id, longBarcode, BarcodeFormat.Code128, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidBarcodeFormat, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_Code128NonPrintableAscii_ReturnsFailure()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Control character (ASCII 1) is not valid for Code 128
        var result = await _sut.AssignBarcodeAsync(
            product.Id, "ABC\x01DEF", BarcodeFormat.Code128, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidBarcodeFormat, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_DuplicateBarcode_ReturnsBarcodeAlreadyAssigned()
    {
        var existingProduct = CreateProduct();
        existingProduct.BarcodeValue = "ABC123";
        existingProduct.BarcodeFormat = "Code128";
        _productRepo.Products.Add(existingProduct);

        var newProduct = CreateProduct();
        _productRepo.Products.Add(newProduct);

        var result = await _sut.AssignBarcodeAsync(
            newProduct.Id, "ABC123", BarcodeFormat.Code128, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BarcodeAlreadyAssigned, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_DuplicateOnDeactivatedProduct_ReturnsBarcodeAlreadyAssigned()
    {
        var deactivated = CreateProduct();
        deactivated.BarcodeValue = "ABC123";
        deactivated.BarcodeFormat = "Code128";
        deactivated.IsDeactivated = true;
        _productRepo.Products.Add(deactivated);

        var newProduct = CreateProduct();
        _productRepo.Products.Add(newProduct);

        var result = await _sut.AssignBarcodeAsync(
            newProduct.Id, "ABC123", BarcodeFormat.Code128, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BarcodeAlreadyAssigned, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_NonExistentProduct_ReturnsFailure()
    {
        var result = await _sut.AssignBarcodeAsync(
            Guid.NewGuid(), "ABC123", BarcodeFormat.Code128, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AssignBarcode_EnqueuesAuditEntry()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        await _sut.AssignBarcodeAsync(
            product.Id, "ABC123", BarcodeFormat.Code128, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        var draft = _auditWriter.EnqueuedDrafts[0];
        Assert.Equal("AssignBarcode", draft.OperationType);
        Assert.Equal("Product", draft.EntityType);
        Assert.Equal(product.Id, draft.EntityId);
        Assert.Null(draft.BeforeState); // no previous barcode
        Assert.Contains("ABC123", draft.AfterState!);
        Assert.Contains("Code128", draft.AfterState!);
    }

    [Fact]
    public async Task AssignBarcode_ReplacingExisting_RecordsPreviousInAudit()
    {
        var product = CreateProduct();
        product.BarcodeValue = "OLDCODE";
        product.BarcodeFormat = "Code128";
        _productRepo.Products.Add(product);

        await _sut.AssignBarcodeAsync(
            product.Id, "NEWCODE1", BarcodeFormat.Code128, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        var draft = _auditWriter.EnqueuedDrafts[0];
        Assert.Contains("OLDCODE", draft.BeforeState!);
        Assert.Contains("NEWCODE1", draft.AfterState!);
    }

    // --- RemoveBarcodeAsync Tests (Req 18.5) ---

    [Fact]
    public async Task RemoveBarcode_ProductWithBarcode_ReturnsSuccess()
    {
        var product = CreateProduct();
        product.BarcodeValue = "ABC123";
        product.BarcodeFormat = "Code128";
        _productRepo.Products.Add(product);

        var result = await _sut.RemoveBarcodeAsync(product.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.BarcodeValue);
        Assert.Null(result.Value.BarcodeFormat);
    }

    [Fact]
    public async Task RemoveBarcode_ProductWithoutBarcode_ReturnsBarcodeNotFound()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        var result = await _sut.RemoveBarcodeAsync(product.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BarcodeNotFound, result.Error!.Value.Code);
    }

    [Fact]
    public async Task RemoveBarcode_NonExistentProduct_ReturnsFailure()
    {
        var result = await _sut.RemoveBarcodeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task RemoveBarcode_EnqueuesAuditEntry()
    {
        var product = CreateProduct();
        product.BarcodeValue = "ABC123";
        product.BarcodeFormat = "Code128";
        _productRepo.Products.Add(product);

        await _sut.RemoveBarcodeAsync(product.Id, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        var draft = _auditWriter.EnqueuedDrafts[0];
        Assert.Equal("RemoveBarcode", draft.OperationType);
        Assert.Equal("Product", draft.EntityType);
        Assert.Equal(product.Id, draft.EntityId);
        Assert.Contains("ABC123", draft.BeforeState!);
        Assert.Null(draft.AfterState);
    }

    // --- GenerateCode128Async Tests (Req 18.17–18.19) ---

    [Fact]
    public async Task GenerateCode128_ValidProduct_ReturnsSuccess()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        var result = await _sut.GenerateCode128Async(product.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.BarcodeValue);
        Assert.Equal(12, result.Value.BarcodeValue!.Length);
        Assert.Equal("Code128", result.Value.BarcodeFormat);
    }

    [Fact]
    public async Task GenerateCode128_GeneratesUppercaseAlphanumeric()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        var result = await _sut.GenerateCode128Async(product.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!.BarcodeValue!.ToCharArray(), c =>
            Assert.True(char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)));
    }

    [Fact]
    public async Task GenerateCode128_NonExistentProduct_ReturnsFailure()
    {
        var result = await _sut.GenerateCode128Async(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task GenerateCode128_EnqueuesAuditEntry()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        await _sut.GenerateCode128Async(product.Id, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        var draft = _auditWriter.EnqueuedDrafts[0];
        Assert.Equal("GenerateBarcode", draft.OperationType);
        Assert.Equal("Product", draft.EntityType);
        Assert.Equal(product.Id, draft.EntityId);
        Assert.Null(draft.BeforeState); // no previous barcode
        Assert.Contains("Code128", draft.AfterState!);
    }

    [Fact]
    public async Task GenerateCode128_ReplacingExisting_RecordsPreviousInAudit()
    {
        var product = CreateProduct();
        product.BarcodeValue = "OLDBARCODE12";
        product.BarcodeFormat = "Code128";
        _productRepo.Products.Add(product);

        await _sut.GenerateCode128Async(product.Id, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        var draft = _auditWriter.EnqueuedDrafts[0];
        Assert.Contains("OLDBARCODE12", draft.BeforeState!);
    }

    [Fact]
    public async Task GenerateCode128_AllCollisions_ReturnsUnexpectedError()
    {
        var product = CreateProduct();
        _productRepo.Products.Add(product);

        // Make ExistsByBarcodeAsync always return true to simulate collisions
        _productRepo.AlwaysExistsByBarcode = true;

        var result = await _sut.GenerateCode128Async(product.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.UnexpectedError, result.Error!.Value.Code);
    }

    // --- Helpers ---

    private static Product CreateProduct() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Product",
        Sku = "SKU-001",
        Description = "Test product",
        CategoryId = Guid.NewGuid(),
        SalePrice = 10.00m,
        CostPrice = 5.00m,
        Quantity = 100,
        MinStockThreshold = 10,
        BarcodeValue = null,
        BarcodeFormat = null,
        IsDeactivated = false,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        RowVersion = Array.Empty<byte>()
    };
}

// --- Fakes for BarcodeService tests ---

internal sealed class FakeProductRepositoryForBarcode : IProductRepository
{
    public List<Product> Products { get; } = new();

    /// <summary>
    /// When true, ExistsByBarcodeAsync always returns true (simulates collisions for generation tests).
    /// </summary>
    public bool AlwaysExistsByBarcode { get; set; }

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
        => Task.FromResult(Products.FirstOrDefault(p =>
            p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)));

    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        => Task.FromResult(Products.FirstOrDefault(p =>
            p.BarcodeValue != null && p.BarcodeValue.Equals(barcode, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default)
        => Task.FromResult(Products.Any(p =>
            p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        if (AlwaysExistsByBarcode)
            return Task.FromResult(true);

        return Task.FromResult(Products.Any(p =>
            p.BarcodeValue != null && p.BarcodeValue.Equals(barcode, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyList<Product>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Product>>(
            Products.Where(p => p.CategoryId == categoryId).ToList().AsReadOnly());

    public Task<IReadOnlyList<Product>> SearchByNameAsync(string term, int maxResults, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Product>>(
            Products.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults).ToList().AsReadOnly());

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Products.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Product>>(Products.AsReadOnly());

    public Task AddAsync(Product entity, CancellationToken ct = default)
    {
        Products.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Product entity) { /* no-op for fake */ }
    public void Remove(Product entity) => Products.Remove(entity);
}

internal sealed class FakeClockForBarcode : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
}

internal sealed class FakeUnitOfWorkForBarcode : IUnitOfWork
{
    public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeAuditWriterForBarcode : IAuditWriter
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
