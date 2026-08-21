using POS.Application.DTOs;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Implements product search by barcode, SKU, or name (Req 18.6-18.10).
/// Case/accent insensitivity for name search is handled by SQL Server collation (Latin1_General_100_CI_AI).
/// </summary>
public sealed class ProductSearchService : IProductSearchService
{
    private const int MaxNameResults = 50;
    private const int RepositoryFetchLimit = 51; // Fetch one extra to detect overflow

    private readonly IProductRepository _productRepository;

    public ProductSearchService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    /// <inheritdoc />
    public async Task<Result<Product>> FindByBarcodeAsync(string barcode, CancellationToken ct)
    {
        var product = await _productRepository.GetByBarcodeAsync(barcode, ct);

        if (product is null)
            return Result<Product>.Failure(ErrorCode.BarcodeNotFound);

        return Result<Product>.Success(product);
    }

    /// <inheritdoc />
    public async Task<Result<Product>> FindBySkuAsync(string sku, CancellationToken ct)
    {
        var product = await _productRepository.GetBySkuAsync(sku, ct);

        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        return Result<Product>.Success(product);
    }

    /// <inheritdoc />
    public async Task<Result<SearchPage>> SearchByNameAsync(string term, CancellationToken ct)
    {
        var results = await _productRepository.SearchByNameAsync(term, RepositoryFetchLimit, ct);

        if (results.Count > MaxNameResults)
        {
            // More than 50 matches exist; return top 50 and signal overflow via TotalCount
            var topItems = results.Take(MaxNameResults).ToList().AsReadOnly();
            return Result<SearchPage>.Success(new SearchPage(topItems, results.Count));
        }

        // 0..50 results: return as-is with exact count
        return Result<SearchPage>.Success(new SearchPage(results, results.Count));
    }
}
