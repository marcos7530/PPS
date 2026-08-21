using POS.Application.Common;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Services;

/// <summary>
/// Handles profit margin resolution (product > category ancestor > global) and price calculation (Req 15).
/// </summary>
public sealed class MarginService : Application.Interfaces.Services.IMarginService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICategoryClosureRepository _categoryClosureRepository;
    private readonly ISystemConfigurationRepository _systemConfigurationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;
    private readonly IClock _clock;

    public MarginService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        ICategoryClosureRepository categoryClosureRepository,
        ISystemConfigurationRepository systemConfigurationRepository,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IClock clock)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _categoryClosureRepository = categoryClosureRepository;
        _systemConfigurationRepository = systemConfigurationRepository;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<EffectiveMargin> ResolveAsync(Guid productId, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
        {
            // Fallback to global if product not found
            var config = await _systemConfigurationRepository.GetAsync(ct);
            var globalMargin = Percentage.Create(config.GlobalProfitMarginPercentage);
            return new EffectiveMargin(globalMargin.Value!, "Global");
        }

        // 1. Check product-level override
        if (product.ProfitMarginPercentage.HasValue)
        {
            var productMargin = Percentage.Create(product.ProfitMarginPercentage.Value);
            return new EffectiveMargin(productMargin.Value!, "Product");
        }

        // 2. Walk up category hierarchy using closure table
        var ancestors = await _categoryClosureRepository.GetAncestorsAsync(product.CategoryId, ct);

        // Order by Depth ASC, skip self-reference (Depth 0), find nearest ancestor with margin
        var orderedAncestors = ancestors
            .Where(a => a.Depth > 0)
            .OrderBy(a => a.Depth)
            .ToList();

        // Also check the category itself (Depth 0 = self)
        var selfClosure = ancestors.FirstOrDefault(a => a.Depth == 0);
        if (selfClosure is not null)
        {
            var selfCategory = await _categoryRepository.GetByIdAsync(selfClosure.AncestorId, ct);
            if (selfCategory?.ProfitMarginPercentage.HasValue == true)
            {
                var catMargin = Percentage.Create(selfCategory.ProfitMarginPercentage.Value);
                return new EffectiveMargin(catMargin.Value!, $"Category:{selfCategory.Name}");
            }
        }

        foreach (var closure in orderedAncestors)
        {
            var category = await _categoryRepository.GetByIdAsync(closure.AncestorId, ct);
            if (category?.ProfitMarginPercentage.HasValue == true)
            {
                var catMargin = Percentage.Create(category.ProfitMarginPercentage.Value);
                return new EffectiveMargin(catMargin.Value!, $"Category:{category.Name}");
            }
        }

        // 3. Fallback to global
        var globalConfig = await _systemConfigurationRepository.GetAsync(ct);
        var fallbackMargin = Percentage.Create(globalConfig.GlobalProfitMarginPercentage);
        return new EffectiveMargin(fallbackMargin.Value!, "Global");
    }

    /// <inheritdoc />
    public Money CalculateSuggestedPrice(Money costPrice, Percentage margin)
    {
        // SuggestedPrice = CostPrice × (1 + Margin / 100)
        var multiplier = 1m + margin.Value / 100m;
        return new Money(costPrice.Amount * multiplier);
    }

    /// <summary>
    /// Sets the global profit margin percentage (Admin only).
    /// </summary>
    public async Task<Result<Unit>> SetGlobalMarginAsync(
        decimal percentage,
        Guid performedBy,
        CancellationToken ct = default)
    {
        var marginResult = Percentage.Create(percentage);
        if (!marginResult.IsSuccess)
            return Result<Unit>.Failure(ErrorCode.InvalidProfitMargin);

        var config = await _systemConfigurationRepository.GetAsync(ct);
        var beforeValue = config.GlobalProfitMarginPercentage;

        config.GlobalProfitMarginPercentage = marginResult.Value!.Value;
        config.UpdatedAt = _clock.UtcNow;
        config.UpdatedBy = performedBy;

        _systemConfigurationRepository.Update(config);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "SetGlobalMargin",
            EntityType: "SystemConfiguration",
            EntityId: null,
            RelatedEntityIds: null,
            BeforeState: $"{{\"globalProfitMarginPercentage\":{beforeValue}}}",
            AfterState: $"{{\"globalProfitMarginPercentage\":{marginResult.Value!.Value}}}",
            Metadata: $"{{\"performed_by\":\"{performedBy}\"}}"));

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

        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Sets a category's profit margin percentage (Manager+). Pass null to clear the override.
    /// </summary>
    public async Task<Result<Category>> SetCategoryMarginAsync(
        Guid categoryId,
        decimal? percentage,
        Guid performedBy,
        CancellationToken ct = default)
    {
        if (percentage.HasValue)
        {
            var marginResult = Percentage.Create(percentage.Value);
            if (!marginResult.IsSuccess)
                return Result<Category>.Failure(ErrorCode.InvalidProfitMargin);
        }

        var category = await _categoryRepository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result<Category>.Failure(ErrorCode.UnexpectedError);

        var beforeValue = category.ProfitMarginPercentage;
        category.ProfitMarginPercentage = percentage.HasValue
            ? Percentage.Create(percentage.Value).Value!.Value
            : null;
        category.UpdatedAt = _clock.UtcNow;

        _categoryRepository.Update(category);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "SetCategoryMargin",
            EntityType: "Category",
            EntityId: categoryId,
            RelatedEntityIds: null,
            BeforeState: $"{{\"profitMarginPercentage\":{FormatNullableDecimal(beforeValue)}}}",
            AfterState: $"{{\"profitMarginPercentage\":{FormatNullableDecimal(category.ProfitMarginPercentage)}}}",
            Metadata: $"{{\"performed_by\":\"{performedBy}\",\"categoryName\":\"{category.Name}\"}}"));

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
    /// Sets a product's profit margin percentage (Manager+). Pass null to clear the override.
    /// </summary>
    public async Task<Result<Product>> SetProductMarginAsync(
        Guid productId,
        decimal? percentage,
        Guid performedBy,
        CancellationToken ct = default)
    {
        if (percentage.HasValue)
        {
            var marginResult = Percentage.Create(percentage.Value);
            if (!marginResult.IsSuccess)
                return Result<Product>.Failure(ErrorCode.InvalidProfitMargin);
        }

        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.UnexpectedError);

        var beforeValue = product.ProfitMarginPercentage;
        product.ProfitMarginPercentage = percentage.HasValue
            ? Percentage.Create(percentage.Value).Value!.Value
            : null;
        product.UpdatedAt = _clock.UtcNow;

        _productRepository.Update(product);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "SetProductMargin",
            EntityType: "Product",
            EntityId: productId,
            RelatedEntityIds: null,
            BeforeState: $"{{\"profitMarginPercentage\":{FormatNullableDecimal(beforeValue)}}}",
            AfterState: $"{{\"profitMarginPercentage\":{FormatNullableDecimal(product.ProfitMarginPercentage)}}}",
            Metadata: $"{{\"performed_by\":\"{performedBy}\",\"productName\":\"{product.Name}\"}}"));

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

        return Result<Product>.Success(product);
    }

    /// <summary>
    /// Recalculates sale prices for affected products using resolved margins.
    /// Excludes manually overridden and deactivated products.
    /// </summary>
    public async Task<Result<int>> RecalculatePricesAsync(
        Guid? categoryId,
        Guid performedBy,
        CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<Product> products;

            if (categoryId.HasValue)
            {
                // Get products in this category and all descendant categories
                var descendantIds = await _categoryClosureRepository.GetDescendantIdsAsync(categoryId.Value, ct);
                var allCategoryIds = new HashSet<Guid>(descendantIds) { categoryId.Value };

                var allProducts = new List<Product>();
                foreach (var catId in allCategoryIds)
                {
                    var catProducts = await _productRepository.GetByCategoryIdAsync(catId, ct);
                    allProducts.AddRange(catProducts);
                }

                products = allProducts;
            }
            else
            {
                products = await _productRepository.GetAllAsync(ct);
            }

            // Filter: exclude manually overridden and deactivated products
            var eligibleProducts = products
                .Where(p => !p.IsPriceManuallyOverridden && !p.IsDeactivated)
                .ToList();

            var updatedCount = 0;

            foreach (var product in eligibleProducts)
            {
                var effectiveMargin = await ResolveAsync(product.Id, ct);
                var costPrice = new Money(product.CostPrice);
                var suggestedPrice = CalculateSuggestedPrice(costPrice, effectiveMargin.Margin);

                if (suggestedPrice.Amount != product.SalePrice)
                {
                    product.SalePrice = suggestedPrice.Amount;
                    product.UpdatedAt = _clock.UtcNow;
                    _productRepository.Update(product);
                    updatedCount++;
                }
            }

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "RecalculatePrices",
                EntityType: "Product",
                EntityId: categoryId,
                RelatedEntityIds: null,
                BeforeState: null,
                AfterState: $"{{\"updatedCount\":{updatedCount},\"totalEligible\":{eligibleProducts.Count}}}",
                Metadata: $"{{\"performed_by\":\"{performedBy}\",\"categoryId\":{FormatNullableGuid(categoryId)}}}"));

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

            return Result<int>.Success(updatedCount);
        }
        catch (Exception)
        {
            return Result<int>.Failure(ErrorCode.PriceRecalculationFailed);
        }
    }

    private static string FormatNullableDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "null";

    private static string FormatNullableGuid(Guid? value) =>
        value.HasValue ? $"\"{value.Value}\"" : "null";
}
