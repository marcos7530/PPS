using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IProductImageRepository : IRepository<ProductImage>
{
    Task<ProductImage?> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
}
