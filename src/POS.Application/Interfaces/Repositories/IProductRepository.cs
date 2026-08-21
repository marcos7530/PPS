using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default);
    Task<bool> ExistsByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByCategoryIdAsync(Guid categoryId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> SearchByNameAsync(string term, int maxResults, CancellationToken ct = default);
}
