using POS.Application.DTOs;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for product search by barcode, SKU, or name (Req 18.6-18.10).
/// </summary>
public interface IProductSearchService
{
    /// <summary>
    /// Finds a product by its barcode value (response time &lt; 1s).
    /// </summary>
    Task<Result<Product>> FindByBarcodeAsync(string barcode, CancellationToken ct);

    /// <summary>
    /// Finds a product by its SKU (response time &lt; 1s).
    /// </summary>
    Task<Result<Product>> FindBySkuAsync(string sku, CancellationToken ct);

    /// <summary>
    /// Searches products by name with accent/case-insensitive matching (response time &lt; 2s, top 50).
    /// </summary>
    Task<Result<SearchPage>> SearchByNameAsync(string term, CancellationToken ct);
}
