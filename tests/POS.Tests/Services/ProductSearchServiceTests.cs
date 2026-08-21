using POS.Application.DTOs;
using POS.Application.Interfaces.Repositories;
using POS.Application.Services;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Tests.Services;

/// <summary>
/// Unit tests for ProductSearchService (Req 18.6-18.10).
/// </summary>
public class ProductSearchServiceTests
{
    private readonly FakeProductRepositoryForSearch _productRepo = new();
    private readonly ProductSearchService _sut;

    public ProductSearchServiceTests()
    {
        _sut = new ProductSearchService(_productRepo);
    }

    // --- FindByBarcodeAsync Tests (Req 18.7, 18.10) ---

    [Fact]
    public async Task FindByBarcode_ExistingBarcode_ReturnsProduct()
    {
        var product = CreateProduct("SKU-001", barcode: "5901234123457");
        _productRepo.Products.Add(product);

        var result = await _sut.FindByBarcodeAsync("5901234123457", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, result.Value!.Id);
        Assert.Equal("5901234123457", result.Value.BarcodeValue);
    }

    [Fact]
    public async Task FindByBarcode_NonExistentBarcode_ReturnsBarcodeNotFound()
    {
        var result = await _sut.FindByBarcodeAsync("0000000000000", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BarcodeNotFound, result.Error!.Value.Code);
    }

    [Fact]
    public async Task FindByBarcode_EmptyRepository_ReturnsBarcodeNotFound()
    {
        var result = await _sut.FindByBarcodeAsync("5901234123457", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.BarcodeNotFound, result.Error!.Value.Code);
    }

    // --- FindBySkuAsync Tests (Req 18.7, 18.10) ---

    [Fact]
    public async Task FindBySku_ExistingSku_ReturnsProduct()
    {
        var product = CreateProduct("SKU-001");
        _productRepo.Products.Add(product);

        var result = await _sut.FindBySkuAsync("SKU-001", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Id, result.Value!.Id);
        Assert.Equal("SKU-001", result.Value.Sku);
    }

    [Fact]
    public async Task FindBySku_NonExistentSku_ReturnsFailure()
    {
        var result = await _sut.FindBySkuAsync("NONEXISTENT", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    [Fact]
    public async Task FindBySku_EmptyRepository_ReturnsFailure()
    {
        var result = await _sut.FindBySkuAsync("SKU-001", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidProductIdentifier, result.Error!.Value.Code);
    }

    // --- SearchByNameAsync Tests (Req 18.8, 18.9, 18.10) ---

    [Fact]
    public async Task SearchByName_MatchingProducts_ReturnsResults()
    {
        _productRepo.Products.Add(CreateProduct("SKU-001", name: "Widget Alpha"));
        _productRepo.Products.Add(CreateProduct("SKU-002", name: "Widget Beta"));
        _productRepo.Products.Add(CreateProduct("SKU-003", name: "Gadget Gamma"));

        var result = await _sut.SearchByNameAsync("Widget", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Items.Count);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task SearchByName_NoMatches_ReturnsEmptyPage()
    {
        _productRepo.Products.Add(CreateProduct("SKU-001", name: "Widget Alpha"));

        var result = await _sut.SearchByNameAsync("NonExistent", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task SearchByName_ExactlyFiftyMatches_ReturnsAll()
    {
        for (int i = 0; i < 50; i++)
            _productRepo.Products.Add(CreateProduct($"SKU-{i:D3}", name: $"Product {i}"));

        var result = await _sut.SearchByNameAsync("Product", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value!.Items.Count);
        Assert.Equal(50, result.Value.TotalCount);
    }

    [Fact]
    public async Task SearchByName_MoreThanFiftyMatches_ReturnsFiftyWithOverflowCount()
    {
        // Add 55 matching products
        for (int i = 0; i < 55; i++)
            _productRepo.Products.Add(CreateProduct($"SKU-{i:D3}", name: $"Product {i}"));

        var result = await _sut.SearchByNameAsync("Product", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value!.Items.Count);
        // TotalCount indicates that more than 50 exist (repository returns 51 when overflow)
        Assert.True(result.Value.TotalCount > 50);
    }

    [Fact]
    public async Task SearchByName_EmptyTerm_ReturnsAllUpToLimit()
    {
        for (int i = 0; i < 5; i++)
            _productRepo.Products.Add(CreateProduct($"SKU-{i:D3}", name: $"Item {i}"));

        var result = await _sut.SearchByNameAsync("", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Items.Count);
    }

    // --- Helpers ---

    private static Product CreateProduct(string sku, string? name = null, string? barcode = null)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = name ?? $"Product {sku}",
            BarcodeValue = barcode,
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
    }
}

// --- Fake for ProductSearchService tests ---

internal sealed class FakeProductRepositoryForSearch : IProductRepository
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
