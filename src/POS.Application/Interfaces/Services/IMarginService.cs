using POS.Application.DTOs;
using POS.Domain.ValueObjects;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for profit margin resolution and price calculation (Req 15).
/// </summary>
public interface IMarginService
{
    /// <summary>
    /// Resolves the effective margin for a product (product override > category ancestor > global).
    /// </summary>
    Task<EffectiveMargin> ResolveAsync(Guid productId, CancellationToken ct);

    /// <summary>
    /// Calculates the suggested sale price from cost price and margin.
    /// </summary>
    Money CalculateSuggestedPrice(Money costPrice, Percentage margin);
}
