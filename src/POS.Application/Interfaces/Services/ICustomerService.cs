using POS.Application.Commands;
using POS.Application.Common;
using POS.Application.Views;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for customer management operations (Req 13).
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Creates a new customer (Req 13.1-13.4).
    /// </summary>
    Task<Result<Customer>> CreateAsync(CreateCustomerCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing customer's details (Req 13.11).
    /// </summary>
    Task<Result<Customer>> UpdateAsync(UpdateCustomerCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Marks a customer as inactive (soft delete) (Req 13.12-13.13).
    /// </summary>
    Task<Result<Unit>> DeactivateAsync(Guid customerId, Guid performedBy, CancellationToken ct = default);

    /// <summary>
    /// Gets a customer by their identifier (Req 13.9).
    /// </summary>
    Task<Result<CustomerView>> GetByIdAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Searches customers by name, email, phone, or identifier (Req 13.5).
    /// Active customers only unless includeInactive is true (Req 13.13).
    /// </summary>
    Task<Result<IReadOnlyList<CustomerSearchResult>>> SearchAsync(
        string? name,
        string? email,
        string? phone,
        Guid? customerId,
        bool includeInactive = false,
        CancellationToken ct = default);

    /// <summary>
    /// Gets customer lifetime statistics (Req 13.14).
    /// </summary>
    Task<Result<CustomerStatistics>> GetStatisticsAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Gets purchase history (last 100 transactions) for a customer (Req 13.9).
    /// </summary>
    Task<Result<IReadOnlyList<CustomerPurchaseHistory>>> GetPurchaseHistoryAsync(
        Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Checks if a phone number is already registered and returns the existing customer name (Req 13.3).
    /// </summary>
    Task<Result<string?>> CheckPhoneDuplicateAsync(string phone, Guid? excludeCustomerId = null, CancellationToken ct = default);
}
