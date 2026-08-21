using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IStoreCreditVoucherRepository : IRepository<StoreCreditVoucher>
{
    Task<StoreCreditVoucher?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<StoreCreditVoucher>> GetByTransactionIdAsync(Guid transactionId, CancellationToken ct = default);
}
