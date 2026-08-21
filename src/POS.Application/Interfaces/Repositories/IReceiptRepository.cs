using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<Receipt?> GetByTransactionIdAsync(Guid transactionId, CancellationToken ct = default);
    Task<Receipt?> GetByReturnIdAsync(Guid returnId, CancellationToken ct = default);
}
