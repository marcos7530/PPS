using POS.Application.Commands;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Manages category hierarchy with closure table maintenance (Req 14).
/// </summary>
public sealed class CategoryTreeService : ICategoryTreeService
{
    private const int MaxDepth = 5;
    private const int NameMaxLength = 100;

    private readonly ICategoryRepository _categoryRepository;
    private readonly ICategoryClosureRepository _closureRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public CategoryTreeService(
        ICategoryRepository categoryRepository,
        ICategoryClosureRepository closureRepository,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _categoryRepository = categoryRepository;
        _closureRepository = closureRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public async Task<Result<Category>> CreateAsync(CreateCategoryCommand cmd, CancellationToken ct)
    {
        // Validate name length
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length < 1 || cmd.Name.Length > NameMaxLength)
            return Result<Category>.Failure(ErrorCode.CategoryNameAlreadyExistsAtLevel);

        // Validate name uniqueness at the same parent level
        if (await _categoryRepository.ExistsByNameAndParentAsync(cmd.Name, cmd.ParentCategoryId, ct))
            return Result<Category>.Failure(ErrorCode.CategoryNameAlreadyExistsAtLevel);

        short depth = 1;
        Category? parent = null;

        if (cmd.ParentCategoryId.HasValue)
        {
            parent = await _categoryRepository.GetByIdAsync(cmd.ParentCategoryId.Value, ct);
            if (parent is null || !parent.IsActive)
                return Result<Category>.Failure(ErrorCode.InvalidParentCategory);

            depth = (short)(parent.Depth + 1);
        }

        // Validate max depth
        if (depth > MaxDepth)
            return Result<Category>.Failure(ErrorCode.MaxCategoryDepthExceeded);

        var now = _clock.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name,
            ParentCategoryId = cmd.ParentCategoryId,
            Description = cmd.Description,
            DisplayOrder = cmd.DisplayOrder,
            ProfitMarginPercentage = cmd.ProfitMarginPercentage,
            Depth = depth,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _categoryRepository.AddAsync(category, ct);

        // Build closure entries: self-reference + ancestor entries
        var closures = new List<CategoryClosure>
        {
            new CategoryClosure
            {
                AncestorId = category.Id,
                DescendantId = category.Id,
                Depth = 0
            }
        };

        if (parent is not null)
        {
            // Get all ancestors of the parent
            var parentAncestors = await _closureRepository.GetAncestorsAsync(parent.Id, ct);
            foreach (var ancestor in parentAncestors)
            {
                closures.Add(new CategoryClosure
                {
                    AncestorId = ancestor.AncestorId,
                    DescendantId = category.Id,
                    Depth = (short)(ancestor.Depth + 1)
                });
            }
        }

        await _closureRepository.AddRangeAsync(closures, ct);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "CreateCategory",
            EntityType: "Category",
            EntityId: category.Id,
            RelatedEntityIds: cmd.ParentCategoryId.HasValue
                ? new List<Guid> { cmd.ParentCategoryId.Value }
                : null,
            BeforeState: null,
            AfterState: $"{{\"name\":\"{category.Name}\",\"depth\":{category.Depth},\"parentId\":\"{category.ParentCategoryId}\"}}",
            Metadata: null));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Category>.Success(category);
    }

    /// <inheritdoc />
    public async Task<Result<Category>> MoveAsync(Guid categoryId, Guid? newParentId, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result<Category>.Failure(ErrorCode.InvalidParentCategory);

        // No-op if already at the target parent
        if (category.ParentCategoryId == newParentId)
            return Result<Category>.Success(category);

        short newDepth = 1;
        Category? newParent = null;

        if (newParentId.HasValue)
        {
            newParent = await _categoryRepository.GetByIdAsync(newParentId.Value, ct);
            if (newParent is null || !newParent.IsActive)
                return Result<Category>.Failure(ErrorCode.InvalidParentCategory);

            // Detect circular reference: new parent cannot be a descendant of the category
            var descendantIds = await _closureRepository.GetDescendantIdsAsync(categoryId, ct);
            if (descendantIds.Contains(newParentId.Value))
                return Result<Category>.Failure(ErrorCode.CircularCategoryReference);

            newDepth = (short)(newParent.Depth + 1);
        }

        // Calculate max subtree depth after move
        // The deepest node in the subtree relative to category is (max_descendant_depth - category.Depth)
        var subtreeDescendantIds = await _closureRepository.GetDescendantIdsAsync(categoryId, ct);
        short maxSubtreeRelativeDepth = 0;

        foreach (var descId in subtreeDescendantIds)
        {
            if (descId == categoryId) continue;

            var descendant = await _categoryRepository.GetByIdAsync(descId, ct);
            if (descendant is not null)
            {
                var relativeDepth = (short)(descendant.Depth - category.Depth);
                if (relativeDepth > maxSubtreeRelativeDepth)
                    maxSubtreeRelativeDepth = relativeDepth;
            }
        }

        if (newDepth + maxSubtreeRelativeDepth > MaxDepth)
            return Result<Category>.Failure(ErrorCode.MaxCategoryDepthExceeded);

        // Check name uniqueness at the new parent level
        if (await _categoryRepository.ExistsByNameAndParentAsync(category.Name, newParentId, ct))
            return Result<Category>.Failure(ErrorCode.CategoryNameAlreadyExistsAtLevel);

        var oldParentId = category.ParentCategoryId;
        var oldDepth = category.Depth;
        var depthDelta = (short)(newDepth - category.Depth);

        // Update the category
        category.ParentCategoryId = newParentId;
        category.Depth = newDepth;
        category.UpdatedAt = _clock.UtcNow;
        _categoryRepository.Update(category);

        // Update depths of all descendants
        foreach (var descId in subtreeDescendantIds)
        {
            if (descId == categoryId) continue;

            var descendant = await _categoryRepository.GetByIdAsync(descId, ct);
            if (descendant is not null)
            {
                descendant.Depth = (short)(descendant.Depth + depthDelta);
                descendant.UpdatedAt = _clock.UtcNow;
                _categoryRepository.Update(descendant);
            }
        }

        // Rebuild closure entries for the subtree
        // Remove old closure entries for all nodes in the subtree (excluding self-references)
        foreach (var descId in subtreeDescendantIds)
        {
            await _closureRepository.RemoveSubtreeAsync(descId, ct);
        }

        // Re-insert closure entries for each node in the subtree
        var newParentAncestors = newParent is not null
            ? await _closureRepository.GetAncestorsAsync(newParent.Id, ct)
            : Array.Empty<CategoryClosure>();

        // Re-insert for the moved category and its descendants
        // First, rebuild the category's own closures
        var allClosures = new List<CategoryClosure>();

        // Self-reference for the moved category
        allClosures.Add(new CategoryClosure
        {
            AncestorId = category.Id,
            DescendantId = category.Id,
            Depth = 0
        });

        // Ancestor closures for the moved category
        foreach (var ancestor in newParentAncestors)
        {
            allClosures.Add(new CategoryClosure
            {
                AncestorId = ancestor.AncestorId,
                DescendantId = category.Id,
                Depth = (short)(ancestor.Depth + 1)
            });
        }

        // Now rebuild closures for each descendant
        // We need to process descendants level by level
        foreach (var descId in subtreeDescendantIds)
        {
            if (descId == categoryId) continue;

            var descendant = await _categoryRepository.GetByIdAsync(descId, ct);
            if (descendant is null) continue;

            // Self-reference
            allClosures.Add(new CategoryClosure
            {
                AncestorId = descId,
                DescendantId = descId,
                Depth = 0
            });

            // Find the descendant's parent and build path up to root
            // The depth relative to category tells us the position in the subtree
            var relativeDepth = descendant.Depth - category.Depth;

            // Add closure to the moved category
            allClosures.Add(new CategoryClosure
            {
                AncestorId = category.Id,
                DescendantId = descId,
                Depth = (short)relativeDepth
            });

            // Add closures to all ancestors above the moved category
            foreach (var ancestor in newParentAncestors)
            {
                allClosures.Add(new CategoryClosure
                {
                    AncestorId = ancestor.AncestorId,
                    DescendantId = descId,
                    Depth = (short)(ancestor.Depth + 1 + relativeDepth)
                });
            }

            // Add closures to intermediate nodes between category and this descendant
            // We need to find all intermediate ancestors within the subtree
            if (descendant.ParentCategoryId.HasValue && descendant.ParentCategoryId.Value != categoryId)
            {
                // Walk up the parent chain within the subtree
                var currentParentId = descendant.ParentCategoryId.Value;
                short distanceFromDesc = 1;

                while (currentParentId != categoryId && subtreeDescendantIds.Contains(currentParentId))
                {
                    allClosures.Add(new CategoryClosure
                    {
                        AncestorId = currentParentId,
                        DescendantId = descId,
                        Depth = distanceFromDesc
                    });

                    var currentParent = await _categoryRepository.GetByIdAsync(currentParentId, ct);
                    if (currentParent?.ParentCategoryId is null) break;
                    currentParentId = currentParent.ParentCategoryId.Value;
                    distanceFromDesc++;
                }
            }
        }

        await _closureRepository.AddRangeAsync(allClosures, ct);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "MoveCategory",
            EntityType: "Category",
            EntityId: category.Id,
            RelatedEntityIds: newParentId.HasValue ? new List<Guid> { newParentId.Value } : null,
            BeforeState: $"{{\"parentId\":\"{oldParentId}\",\"depth\":{oldDepth}}}",
            AfterState: $"{{\"parentId\":\"{newParentId}\",\"depth\":{category.Depth}}}",
            Metadata: null));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Category>.Success(category);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid categoryId, CancellationToken ct)
    {
        return await _closureRepository.GetDescendantIdsAsync(categoryId, ct);
    }

    /// <summary>
    /// Updates a category's editable fields (name, description, display order, profit margin).
    /// </summary>
    public async Task<Result<Category>> UpdateAsync(
        Guid categoryId,
        string name,
        string? description,
        int displayOrder,
        decimal? profitMarginPercentage,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 1 || name.Length > NameMaxLength)
            return Result<Category>.Failure(ErrorCode.CategoryNameAlreadyExistsAtLevel);

        var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result<Category>.Failure(ErrorCode.InvalidParentCategory);

        // Check name uniqueness if name changed (case-insensitive handled by collation)
        if (!string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            if (await _categoryRepository.ExistsByNameAndParentAsync(name, category.ParentCategoryId, ct))
                return Result<Category>.Failure(ErrorCode.CategoryNameAlreadyExistsAtLevel);
        }

        var beforeState = $"{{\"name\":\"{category.Name}\",\"description\":\"{category.Description}\",\"displayOrder\":{category.DisplayOrder},\"profitMargin\":{category.ProfitMarginPercentage}}}";

        category.Name = name;
        category.Description = description;
        category.DisplayOrder = displayOrder;
        category.ProfitMarginPercentage = profitMarginPercentage;
        category.UpdatedAt = _clock.UtcNow;

        _categoryRepository.Update(category);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "UpdateCategory",
            EntityType: "Category",
            EntityId: category.Id,
            RelatedEntityIds: null,
            BeforeState: beforeState,
            AfterState: $"{{\"name\":\"{category.Name}\",\"description\":\"{category.Description}\",\"displayOrder\":{category.DisplayOrder},\"profitMargin\":{category.ProfitMarginPercentage}}}",
            Metadata: null));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Category>.Success(category);
    }

    /// <summary>
    /// Deactivates a category and all its descendants (cascade).
    /// </summary>
    public async Task<Result<Category>> DeactivateAsync(Guid categoryId, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result<Category>.Failure(ErrorCode.InvalidParentCategory);

        var descendantIds = await _closureRepository.GetDescendantIdsAsync(categoryId, ct);
        var now = _clock.UtcNow;

        // Deactivate the category itself
        category.IsActive = false;
        category.UpdatedAt = now;
        _categoryRepository.Update(category);

        // Deactivate all descendants
        var deactivatedIds = new List<Guid> { categoryId };
        foreach (var descId in descendantIds)
        {
            if (descId == categoryId) continue;

            var descendant = await _categoryRepository.GetByIdAsync(descId, ct);
            if (descendant is not null && descendant.IsActive)
            {
                descendant.IsActive = false;
                descendant.UpdatedAt = now;
                _categoryRepository.Update(descendant);
                deactivatedIds.Add(descId);
            }
        }

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "DeactivateCategory",
            EntityType: "Category",
            EntityId: category.Id,
            RelatedEntityIds: deactivatedIds.Count > 1 ? deactivatedIds.Skip(1).ToList() : null,
            BeforeState: "{\"isActive\":true}",
            AfterState: $"{{\"isActive\":false,\"cascadedCount\":{deactivatedIds.Count}}}",
            Metadata: null));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Category>.Success(category);
    }

    /// <summary>
    /// Gets a category by its ID.
    /// </summary>
    public async Task<Result<Category>> GetByIdAsync(Guid categoryId, CancellationToken ct)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result<Category>.Failure(ErrorCode.InvalidParentCategory);

        return Result<Category>.Success(category);
    }

    /// <summary>
    /// Gets all root categories (tree roots).
    /// </summary>
    public async Task<Result<IReadOnlyList<Category>>> GetTreeAsync(CancellationToken ct)
    {
        var roots = await _categoryRepository.GetRootsAsync(ct);
        return Result<IReadOnlyList<Category>>.Success(roots);
    }
}
