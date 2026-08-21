using POS.Application.Commands;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Tests.Services;

/// <summary>
/// Unit tests for InventoryService (Req 10.1–10.10).
/// </summary>
public class InventoryServiceTests : IDisposable
{
    private readonly FakeProductRepository _productRepo = new();
    private readonly FakeCategoryRepositoryForInventory _categoryRepo = new();
    private readonly FakeClockForInventory _clock = new();
    private readonly FakeUnitOfWorkForInventory _unitOfWork = new();
    private readonly FakeAuditWriterForInventory _auditWriter = new();
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        _sut = new InventoryService(
            _productRepo,
            _categoryRepo,
            _clock,
            _unitOfWork,
            _auditWriter);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- CreateAsync Tests (Req 10.1, 10.8, 10.9) ---

    [Fact]
    public async Task Create_ValidInput_ReturnsSuccess()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = CreateValidProductCommand();
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Test Product", result.Value!.Name);
        Assert.Equal("SKU-001", result.Value.Sku);
        Assert.Equal(10.00m, result.Value.SalePrice);
        Assert.Equal(5.00m, result.Value.CostPrice);
        Assert.Equal(100, result.Value.Quantity);
        Assert.Equal(10, result.Value.MinStockThreshold);
        Assert.False(result.Value.IsDeactivated);
    }

    [Fact]
    public async Task Create_ValidInput_AddsProductToRepository()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = CreateValidProductCommand();
        await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.Single(_productRepo.Products);
    }

    [Fact]
    public async Task Create_ValidInput_EnqueuesAuditEntry()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = CreateValidProductCommand();
        await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("CreateProduct", _auditWriter.EnqueuedDrafts[0].OperationType);
        Assert.Equal("Product", _auditWriter.EnqueuedDrafts[0].EntityType);
    }

    [Fact]
    public async Task Create_DuplicateSku_ReturnsDuplicateSku()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());
        _productRepo.Products.Add(CreateProduct("SKU-001"));

        var cmd = CreateValidProductCommand();
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.DuplicateSku, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Create_DuplicateSkuOnDeactivatedProduct_ReturnsDuplicateSku()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());
        var deactivated = CreateProduct("SKU-001");
        deactivated.IsDeactivated = true;
        _productRepo.Products.Add(deactivated);

        var cmd = CreateValidProductCommand();
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.DuplicateSku, result.Error!.Value.Code);
    }

    [Theory]
    [InlineData("")] // empty name
    [InlineData("   ")] // whitespace name
    public async Task Create_InvalidName_ReturnsFailure(string name)
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = new CreateProductCommand(name, "SKU-001", null, 10.00m, 5.00m, _categoryRepo.Categories[0].Id, 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Create_NameTooLong_ReturnsFailure()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var longName = new string('a', 101);
        var cmd = new CreateProductCommand(longName, "SKU-001", null, 10.00m, 5.00m, _categoryRepo.Categories[0].Id, 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Theory]
    [InlineData("")] // empty SKU
    [InlineData("   ")] // whitespace SKU
    public async Task Create_InvalidSku_ReturnsFailure(string sku)
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = new CreateProductCommand("Product", sku, null, 10.00m, 5.00m, _categoryRepo.Categories[0].Id, 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Create_SkuTooLong_ReturnsFailure()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var longSku = new string('X', 51);
        var cmd = new CreateProductCommand("Product", longSku, null, 10.00m, 5.00m, _categoryRepo.Categories[0].Id, 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Theory]
    [InlineData(0.00)] // below min
    [InlineData(1_000_000.00)] // above max
    public async Task Create_InvalidSalePrice_ReturnsFailure(decimal price)
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = new CreateProductCommand("Product", "SKU-001", null, price, 5.00m, _categoryRepo.Categories[0].Id, 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCostPrice, result.Error!.Value.Code);
    }

    [Theory]
    [InlineData(0.00)] // below min
    [InlineData(1_000_000.00)] // above max
    public async Task Create_InvalidCostPrice_ReturnsFailure(decimal price)
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = new CreateProductCommand("Product", "SKU-001", null, 10.00m, price, _categoryRepo.Categories[0].Id, 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCostPrice, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Create_QuantityBelowMin_ReturnsFailure()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = new CreateProductCommand("Product", "SKU-001", null, 10.00m, 5.00m, _categoryRepo.Categories[0].Id, -1, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Create_QuantityAboveMax_ReturnsFailure()
    {
        _categoryRepo.Categories.Add(CreateActiveCategory());

        var cmd = new CreateProductCommand("Product", "SKU-001", null, 10.00m, 5.00m, _categoryRepo.Categories[0].Id, 1_000_000, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Create_NonExistentCategory_ReturnsFailure()
    {
        var cmd = new CreateProductCommand("Product", "SKU-001", null, 10.00m, 5.00m, Guid.NewGuid(), 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidParentCategory, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Create_InactiveCategory_ReturnsFailure()
    {
        var inactiveCategory = CreateActiveCategory();
        inactiveCategory.IsActive = false;
        _categoryRepo.Categories.Add(inactiveCategory);

        var cmd = new CreateProductCommand("Product", "SKU-001", null, 10.00m, 5.00m, inactiveCategory.Id, 100, 10);
        var result = await _sut.CreateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidParentCategory, result.Error!.Value.Code);
    }

    // --- UpdateAsync Tests (Req 10.3) ---

    [Fact]
    public async Task Update_ValidInput_ReturnsSuccess()
    {
        var category = CreateActiveCategory();
        _categoryRepo.Categories.Add(category);
        var product = CreateProduct("SKU-001");
        product.CategoryId = category.Id;
        _productRepo.Products.Add(product);

        var cmd = new UpdateProductCommand(product.Id, "Updated Name", "New desc", 20.00m, 10.00m, category.Id, 200, 20);
        var result = await _sut.UpdateAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", result.Value!.Name);
        Assert.Equal("New desc", result.Value.Description);
        Assert.Equal(20.00m, result.Value.SalePrice);
        Assert.Equal(10.00m, result.Value.CostPrice);
        Assert.Equal(200, result.Value.Quantity);
        Assert.Equal(20, result.Value.MinStockThreshold);
    }

    [Fact]
    public async Task Update_ValidInput_EnqueuesAuditEntry()
    {
        var category = CreateActiveCategory();
        _categoryRepo.Categories.Add(category);
        var product = CreateProduct("SKU-001");
        product.CategoryId = category.Id;
        _productRepo.Products.Add(product);

        var cmd = new UpdateProductCommand(product.Id, "Updated", null, 20.00m, 10.00m, category.Id, 200, 20);
        await _sut.UpdateAsync(cmd, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("UpdateProduct", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    [Fact]
    public async Task Update_NonExistentProduct_ReturnsFailure()
    {
        var category = CreateActiveCategory();
        _categoryRepo.Categories.Add(category);

        var cmd = new UpdateProductCommand(Guid.NewGuid(), "Name", null, 10.00m, 5.00m, category.Id, 100, 10);
        var result = await _sut.UpdateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Update_InvalidPrice_ReturnsFailure()
    {
        var category = CreateActiveCategory();
        _categoryRepo.Categories.Add(category);
        var product = CreateProduct("SKU-001");
        product.CategoryId = category.Id;
        _productRepo.Products.Add(product);

        var cmd = new UpdateProductCommand(product.Id, "Name", null, 0.00m, 5.00m, category.Id, 100, 10);
        var result = await _sut.UpdateAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCostPrice, result.Error!.Value.Code);
    }

    // --- AdjustQuantityAsync Tests (Req 10.6) ---

    [Fact]
    public async Task AdjustQuantity_ValidInput_ReturnsSuccess()
    {
        var product = CreateProduct("SKU-001");
        product.Quantity = 50;
        _productRepo.Products.Add(product);

        var cmd = new AdjustQuantityCommand(product.Id, 75, QuantityAdjustmentReason.Restock);
        var result = await _sut.AdjustQuantityAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(75, result.Value!.Quantity);
    }

    [Fact]
    public async Task AdjustQuantity_RecordsReasonInAudit()
    {
        var product = CreateProduct("SKU-001");
        product.Quantity = 50;
        _productRepo.Products.Add(product);

        var cmd = new AdjustQuantityCommand(product.Id, 45, QuantityAdjustmentReason.Damage);
        await _sut.AdjustQuantityAsync(cmd, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        var draft = _auditWriter.EnqueuedDrafts[0];
        Assert.Equal("AdjustProductQuantity", draft.OperationType);
        Assert.Contains("\"reason\":\"Damage\"", draft.Metadata!);
        Assert.Contains("\"quantity\":50", draft.BeforeState!);
        Assert.Contains("\"quantity\":45", draft.AfterState!);
    }

    [Fact]
    public async Task AdjustQuantity_NonExistentProduct_ReturnsFailure()
    {
        var cmd = new AdjustQuantityCommand(Guid.NewGuid(), 50, QuantityAdjustmentReason.Correction);
        var result = await _sut.AdjustQuantityAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task AdjustQuantity_InvalidQuantity_ReturnsFailure()
    {
        var product = CreateProduct("SKU-001");
        _productRepo.Products.Add(product);

        var cmd = new AdjustQuantityCommand(product.Id, -1, QuantityAdjustmentReason.Correction);
        var result = await _sut.AdjustQuantityAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task AdjustQuantity_QuantityAboveMax_ReturnsFailure()
    {
        var product = CreateProduct("SKU-001");
        _productRepo.Products.Add(product);

        var cmd = new AdjustQuantityCommand(product.Id, 1_000_000, QuantityAdjustmentReason.Restock);
        var result = await _sut.AdjustQuantityAsync(cmd, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    // --- DeactivateAsync Tests (Req 10.4) ---

    [Fact]
    public async Task Deactivate_ExistingProduct_SetsIsDeactivatedTrue()
    {
        var product = CreateProduct("SKU-001");
        _productRepo.Products.Add(product);

        var result = await _sut.DeactivateAsync(product.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsDeactivated);
    }

    [Fact]
    public async Task Deactivate_EnqueuesAuditEntry()
    {
        var product = CreateProduct("SKU-001");
        _productRepo.Products.Add(product);

        await _sut.DeactivateAsync(product.Id, CancellationToken.None);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("DeactivateProduct", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    [Fact]
    public async Task Deactivate_NonExistentProduct_ReturnsFailure()
    {
        var result = await _sut.DeactivateAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    // --- ValidateForTransactionAsync Tests (Req 10.5) ---

    [Fact]
    public async Task ValidateForTransaction_ActiveProduct_ReturnsSuccess()
    {
        var product = CreateProduct("SKU-001");
        _productRepo.Products.Add(product);

        var result = await _sut.ValidateForTransactionAsync(product.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateForTransaction_DeactivatedProduct_ReturnsProductNoLongerAvailable()
    {
        var product = CreateProduct("SKU-001");
        product.IsDeactivated = true;
        _productRepo.Products.Add(product);

        var result = await _sut.ValidateForTransactionAsync(product.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ProductNoLongerAvailable, result.Error!.Value.Code);
    }

    [Fact]
    public async Task ValidateForTransaction_NonExistentProduct_ReturnsFailure()
    {
        var result = await _sut.ValidateForTransactionAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    // --- IsLowStock Tests (Req 10.2) ---

    [Theory]
    [InlineData(10, 10, true)]  // quantity == threshold
    [InlineData(5, 10, true)]   // quantity < threshold
    [InlineData(11, 10, false)] // quantity > threshold
    [InlineData(0, 0, true)]    // both zero
    public void IsLowStock_ReturnsCorrectResult(int quantity, int threshold, bool expected)
    {
        var product = CreateProduct("SKU-001");
        product.Quantity = quantity;
        product.MinStockThreshold = threshold;

        var result = InventoryService.IsLowStock(product);

        Assert.Equal(expected, result);
    }

    // --- GetActiveProductsAsync Tests (Req 10.10) ---

    [Fact]
    public async Task GetActiveProducts_ExcludesDeactivated()
    {
        var active = CreateProduct("SKU-001");
        var deactivated = CreateProduct("SKU-002");
        deactivated.IsDeactivated = true;
        _productRepo.Products.Add(active);
        _productRepo.Products.Add(deactivated);

        var result = await _sut.GetActiveProductsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("SKU-001", result.Value![0].Sku);
    }

    // --- Helpers ---

    private static readonly Guid DefaultCategoryId = Guid.NewGuid();

    private static Category CreateActiveCategory() => new()
    {
        Id = DefaultCategoryId,
        Name = "Electronics",
        IsActive = true,
        Depth = 1,
        DisplayOrder = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static CreateProductCommand CreateValidProductCommand() => new(
        Name: "Test Product",
        Sku: "SKU-001",
        Description: "A test product",
        SalePrice: 10.00m,
        CostPrice: 5.00m,
        CategoryId: DefaultCategoryId,
        Quantity: 100,
        MinStockThreshold: 10);

    private static Product CreateProduct(string sku) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Product {sku}",
        Sku = sku,
        Description = "Test product",
        CategoryId = DefaultCategoryId,
        SalePrice = 10.00m,
        CostPrice = 5.00m,
        Quantity = 100,
        MinStockThreshold = 10,
        IsDeactivated = false,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        RowVersion = Array.Empty<byte>()
    };
}

// --- Fakes for InventoryService tests ---

internal sealed class FakeProductRepository : IProductRepository
{
    public List<Product> Products { get; } = new();

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
        => Task.FromResult(Products.Any(p =>
            p.BarcodeValue != null && p.BarcodeValue.Equals(barcode, StringComparison.OrdinalIgnoreCase)));

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

internal sealed class FakeCategoryRepositoryForInventory : ICategoryRepository
{
    public List<Category> Categories { get; } = new();

    public Task<bool> ExistsByNameAndParentAsync(string name, Guid? parentCategoryId, CancellationToken ct = default)
        => Task.FromResult(Categories.Any(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            c.ParentCategoryId == parentCategoryId));

    public Task<IReadOnlyList<Category>> GetChildrenAsync(Guid parentId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Category>>(
            Categories.Where(c => c.ParentCategoryId == parentId).ToList().AsReadOnly());

    public Task<IReadOnlyList<Category>> GetRootsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Category>>(
            Categories.Where(c => c.ParentCategoryId == null).ToList().AsReadOnly());

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Category>>(Categories.AsReadOnly());

    public Task AddAsync(Category entity, CancellationToken ct = default)
    {
        Categories.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Category entity) { /* no-op */ }
    public void Remove(Category entity) => Categories.Remove(entity);
}

internal sealed class FakeClockForInventory : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
}

internal sealed class FakeUnitOfWorkForInventory : IUnitOfWork
{
    public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeAuditWriterForInventory : IAuditWriter
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
