using POS.Application.Views;
using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Checks if email exists, optionally excluding a specific customer (for updates).
    /// </summary>
    Task<bool> ExistsByEmailAsync(string email, Guid excludeCustomerId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a normalized phone number exists in the system.
    /// </summary>
    Task<bool> ExistsByPhoneNormalizedAsync(string phoneNormalized, CancellationToken ct = default);

    /// <summary>
    /// Checks if a normalized phone exists excluding a specific customer (for updates).
    /// </summary>
    Task<bool> ExistsByPhoneNormalizedAsync(string phoneNormalized, Guid excludeCustomerId, CancellationToken ct = default);

    /// <summary>
    /// Gets the customer name that has the given normalized phone number.
    /// </summary>
    Task<string?> GetNameByPhoneNormalizedAsync(string phoneNormalized, CancellationToken ct = default);

    /// <summary>
    /// Gets the customer name by normalized phone excluding a specific customer.
    /// </summary>
    Task<string?> GetNameByPhoneNormalizedAsync(string phoneNormalized, Guid excludeCustomerId, CancellationToken ct = default);

    /// <summary>
    /// Searches customers by partial name match (CI/AI via collation) (Req 13.5).
    /// </summary>
    Task<IReadOnlyList<Customer>> SearchByNameAsync(string name, bool includeInactive, CancellationToken ct = default);

    /// <summary>
    /// Searches customers by partial phone match (Req 13.5).
    /// </summary>
    Task<IReadOnlyList<Customer>> SearchByPhoneAsync(string phone, bool includeInactive, CancellationToken ct = default);

    /// <summary>
    /// Gets the total number of non-voided transactions for a customer (Req 13.14).
    /// </summary>
    Task<int> GetTransactionCountAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Gets the sum of FinalAmount of non-voided transactions for a customer (Req 13.14).
    /// </summary>
    Task<decimal> GetTotalPurchaseAmountAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent transaction date for a customer (Req 13.14).
    /// </summary>
    Task<DateTimeOffset?> GetLastPurchaseDateAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Gets purchase history (last 100 transactions) for a customer (Req 13.9).
    /// </summary>
    Task<IReadOnlyList<CustomerPurchaseHistory>> GetPurchaseHistoryAsync(Guid customerId, CancellationToken ct = default);
}
