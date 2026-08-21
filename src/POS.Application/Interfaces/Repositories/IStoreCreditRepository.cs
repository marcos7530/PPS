using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IStoreCreditRepository : IRepository<StoreCredit>
{
    Task<StoreCredit?> GetByCustomerIdAsync(Guid customerId, CancellationToken ct = default);
}
