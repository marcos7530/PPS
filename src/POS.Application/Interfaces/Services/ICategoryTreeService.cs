using POS.Application.Commands;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for category hierarchy management (Req 14).
/// </summary>
public interface ICategoryTreeService
{
    /// <summary>
    /// Creates a new category, validating depth constraint (max 5).
    /// </summary>
    Task<Result<Category>> CreateAsync(CreateCategoryCommand cmd, CancellationToken ct);

    /// <summary>
    /// Moves a category to a new parent, validating circular references and depth.
    /// </summary>
    Task<Result<Category>> MoveAsync(Guid categoryId, Guid? newParentId, CancellationToken ct);

    /// <summary>
    /// Gets all descendant category IDs using the closure table.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid categoryId, CancellationToken ct);
}
