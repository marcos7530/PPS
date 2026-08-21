using POS.Application.Commands;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Manages product CRUD, inventory adjustments, and deactivation (Req 10).
/// </summary>
public sealed class InventoryService
{
    private const int NameMaxLength = 100;
    private const int SkuMaxLength = 50;
    private const int DescriptionMaxLength = 500;
    private const decimal PriceMin = 0.01m;
    private const decimal PriceMax = 999_999.99m;
    private const int QuantityMin = 0;
    private const int QuantityMax = 999_999;

    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public InventoryService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Creates a new product (Req 10.1, 10.8, 10.9).
    /// </summary>
    public async Task<Result<Product>> CreateAsync(CreateProductCommand cmd, CancellationToken ct)
    {
        // Validate name
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > NameMaxLength)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        // Validate SKU
        if (string.IsNullOrWhiteSpace(cmd.Sku) || cmd.Sku.Length > SkuMaxLength)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        // Validate price ranges
        if (cmd.SalePrice < PriceMin || cmd.SalePrice > PriceMax)
            return Result<Product>.Failure(ErrorCode.InvalidCostPrice);

        if (cmd.CostPrice < PriceMin || cmd.CostPrice > PriceMax)
            return Result<Product>.Failure(ErrorCode.InvalidCostPrice);

        // Validate quantity ranges
        if (cmd.Quantity < QuantityMin || cmd.Quantity > QuantityMax)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        if (cmd.MinStockThreshold < QuantityMin || cmd.MinStockThreshold > QuantityMax)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        // Validate SKU uniqueness (including deactivated products) - Req 10.8, 10.9
        if (await _productRepository.ExistsBySkuAsync(cmd.Sku, ct))
            return Result<Product>.Failure(ErrorCode.DuplicateSku);

        // Validate category exists and is active
        var category = await _categoryRepository.GetByIdAsync(cmd.CategoryId, ct);
        if (category is null || !category.IsActive)
            return Result<Product>.Failure(ErrorCode.InvalidParentCategory);

        var now = _clock.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name,
            Sku = cmd.Sku,
            Description = cmd.Description,
            CategoryId = cmd.CategoryId,
            SalePrice = cmd.SalePrice,
            CostPrice = cmd.CostPrice,
            Quantity = cmd.Quantity,
            MinStockThreshold = cmd.MinStockThreshold,
            IsDeactivated = false,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = Array.Empty<byte>()
        };

        await _productRepository.AddAsync(product, ct);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "CreateProduct",
            EntityType: "Product",
            EntityId: product.Id,
            RelatedEntityIds: new List<Guid> { cmd.CategoryId },
            BeforeState: null,
            AfterState: $"{{\"sku\":\"{product.Sku}\",\"name\":\"{product.Name}\",\"salePrice\":{product.SalePrice},\"costPrice\":{product.CostPrice},\"quantity\":{product.Quantity},\"minStock\":{product.MinStockThreshold},\"categoryId\":\"{product.CategoryId}\"}}",
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

        return Result<Product>.Success(product);
    }

    /// <summary>
    /// Updates an existing product's fields (Req 10.3).
    /// </summary>
    public async Task<Result<Product>> UpdateAsync(UpdateProductCommand cmd, CancellationToken ct)
    {
        // Validate name
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length > NameMaxLength)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        // Validate price ranges
        if (cmd.SalePrice < PriceMin || cmd.SalePrice > PriceMax)
            return Result<Product>.Failure(ErrorCode.InvalidCostPrice);

        if (cmd.CostPrice < PriceMin || cmd.CostPrice > PriceMax)
            return Result<Product>.Failure(ErrorCode.InvalidCostPrice);

        // Validate quantity ranges
        if (cmd.Quantity < QuantityMin || cmd.Quantity > QuantityMax)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        if (cmd.MinStockThreshold < QuantityMin || cmd.MinStockThreshold > QuantityMax)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        var product = await _productRepository.GetByIdAsync(cmd.ProductId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        // Validate category exists and is active
        var category = await _categoryRepository.GetByIdAsync(cmd.CategoryId, ct);
        if (category is null || !category.IsActive)
            return Result<Product>.Failure(ErrorCode.InvalidParentCategory);

        var beforeState = $"{{\"name\":\"{product.Name}\",\"description\":\"{product.Description}\",\"salePrice\":{product.SalePrice},\"costPrice\":{product.CostPrice},\"quantity\":{product.Quantity},\"minStock\":{product.MinStockThreshold},\"categoryId\":\"{product.CategoryId}\"}}";

        product.Name = cmd.Name;
        product.Description = cmd.Description;
        product.SalePrice = cmd.SalePrice;
        product.CostPrice = cmd.CostPrice;
        product.CategoryId = cmd.CategoryId;
        product.Quantity = cmd.Quantity;
        product.MinStockThreshold = cmd.MinStockThreshold;
        product.UpdatedAt = _clock.UtcNow;

        _productRepository.Update(product);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "UpdateProduct",
            EntityType: "Product",
            EntityId: product.Id,
            RelatedEntityIds: null,
            BeforeState: beforeState,
            AfterState: $"{{\"name\":\"{product.Name}\",\"description\":\"{product.Description}\",\"salePrice\":{product.SalePrice},\"costPrice\":{product.CostPrice},\"quantity\":{product.Quantity},\"minStock\":{product.MinStockThreshold},\"categoryId\":\"{product.CategoryId}\"}}",
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

        return Result<Product>.Success(product);
    }

    /// <summary>
    /// Adjusts product quantity with reason for audit trail (Req 10.6).
    /// </summary>
    public async Task<Result<Product>> AdjustQuantityAsync(AdjustQuantityCommand cmd, CancellationToken ct)
    {
        // Validate new quantity
        if (cmd.NewQuantity < QuantityMin || cmd.NewQuantity > QuantityMax)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        var product = await _productRepository.GetByIdAsync(cmd.ProductId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        var previousQuantity = product.Quantity;
        product.Quantity = cmd.NewQuantity;
        product.UpdatedAt = _clock.UtcNow;

        _productRepository.Update(product);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "AdjustProductQuantity",
            EntityType: "Product",
            EntityId: product.Id,
            RelatedEntityIds: null,
            BeforeState: $"{{\"quantity\":{previousQuantity}}}",
            AfterState: $"{{\"quantity\":{product.Quantity}}}",
            Metadata: $"{{\"reason\":\"{cmd.Reason}\"}}"));

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
    /// Soft-deactivates a product without deleting (Req 10.4, 10.10).
    /// </summary>
    public async Task<Result<Product>> DeactivateAsync(Guid productId, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        product.IsDeactivated = true;
        product.UpdatedAt = _clock.UtcNow;

        _productRepository.Update(product);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "DeactivateProduct",
            EntityType: "Product",
            EntityId: product.Id,
            RelatedEntityIds: null,
            BeforeState: "{\"isDeactivated\":false}",
            AfterState: "{\"isDeactivated\":true}",
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

        return Result<Product>.Success(product);
    }

    /// <summary>
    /// Validates that a product is active and available for use in a transaction (Req 10.5).
    /// </summary>
    public async Task<Result<Product>> ValidateForTransactionAsync(Guid productId, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        if (product.IsDeactivated)
            return Result<Product>.Failure(ErrorCode.ProductNoLongerAvailable);

        return Result<Product>.Success(product);
    }

    /// <summary>
    /// Gets a product by ID.
    /// </summary>
    public async Task<Result<Product>> GetByIdAsync(Guid productId, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        return Result<Product>.Success(product);
    }

    /// <summary>
    /// Gets all active products (excludes deactivated).
    /// </summary>
    public async Task<Result<IReadOnlyList<Product>>> GetActiveProductsAsync(CancellationToken ct)
    {
        var all = await _productRepository.GetAllAsync(ct);
        var active = all.Where(p => !p.IsDeactivated).ToList();
        return Result<IReadOnlyList<Product>>.Success(active);
    }

    /// <summary>
    /// Checks if a product is in low stock (Req 10.2).
    /// </summary>
    public static bool IsLowStock(Product product)
    {
        return product.Quantity <= product.MinStockThreshold;
    }
}
