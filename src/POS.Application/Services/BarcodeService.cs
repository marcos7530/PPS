using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Services;

/// <summary>
/// Manages barcode assignment, removal, and Code 128 generation for products (Req 18.1–18.5, 18.17–18.19).
/// </summary>
public sealed class BarcodeService
{
    private const int Code128GeneratedLength = 12;
    private const int MaxGenerationAttempts = 10;

    // Characters used for Code 128 generation: uppercase letters and digits for readability
    private static readonly char[] GenerationCharset =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

    private readonly IProductRepository _productRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public BarcodeService(
        IProductRepository productRepository,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _productRepository = productRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Assigns a barcode to a product after validation and uniqueness check (Req 18.1–18.4).
    /// </summary>
    public async Task<Result<Product>> AssignBarcodeAsync(
        Guid productId, string barcodeValue, BarcodeFormat format, CancellationToken ct)
    {
        // Validate barcode format and check digit
        var barcodeResult = Barcode.Create(barcodeValue, format);
        if (!barcodeResult.IsSuccess)
            return Result<Product>.Failure(barcodeResult.Error!.Value);

        // Check uniqueness across all products (including deactivated)
        if (await _productRepository.ExistsByBarcodeAsync(barcodeValue, ct))
            return Result<Product>.Failure(ErrorCode.BarcodeAlreadyAssigned);

        // Get the product
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        var previousBarcode = product.BarcodeValue;
        var previousFormat = product.BarcodeFormat;

        // Assign the barcode
        product.BarcodeValue = barcodeValue;
        product.BarcodeFormat = format.ToString();
        product.UpdatedAt = _clock.UtcNow;

        _productRepository.Update(product);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "AssignBarcode",
            EntityType: "Product",
            EntityId: product.Id,
            RelatedEntityIds: null,
            BeforeState: previousBarcode is not null
                ? $"{{\"barcodeValue\":\"{previousBarcode}\",\"barcodeFormat\":\"{previousFormat}\"}}"
                : null,
            AfterState: $"{{\"barcodeValue\":\"{barcodeValue}\",\"barcodeFormat\":\"{format}\"}}",
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
    /// Removes the barcode from a product (Req 18.5).
    /// </summary>
    public async Task<Result<Product>> RemoveBarcodeAsync(Guid productId, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        if (product.BarcodeValue is null)
            return Result<Product>.Failure(ErrorCode.BarcodeNotFound);

        var previousBarcode = product.BarcodeValue;
        var previousFormat = product.BarcodeFormat;

        product.BarcodeValue = null;
        product.BarcodeFormat = null;
        product.UpdatedAt = _clock.UtcNow;

        _productRepository.Update(product);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "RemoveBarcode",
            EntityType: "Product",
            EntityId: product.Id,
            RelatedEntityIds: null,
            BeforeState: $"{{\"barcodeValue\":\"{previousBarcode}\",\"barcodeFormat\":\"{previousFormat}\"}}",
            AfterState: null,
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
    /// Generates a unique 12-char Code 128 barcode and assigns it to a product (Req 18.17–18.19).
    /// </summary>
    public async Task<Result<Product>> GenerateCode128Async(Guid productId, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<Product>.Failure(ErrorCode.InvalidProductIdentifier);

        string? generatedBarcode = null;
        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var candidate = GenerateRandomCode128();
            if (!await _productRepository.ExistsByBarcodeAsync(candidate, ct))
            {
                generatedBarcode = candidate;
                break;
            }
        }

        if (generatedBarcode is null)
            return Result<Product>.Failure(ErrorCode.UnexpectedError);

        var previousBarcode = product.BarcodeValue;
        var previousFormat = product.BarcodeFormat;

        product.BarcodeValue = generatedBarcode;
        product.BarcodeFormat = BarcodeFormat.Code128.ToString();
        product.UpdatedAt = _clock.UtcNow;

        _productRepository.Update(product);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "GenerateBarcode",
            EntityType: "Product",
            EntityId: product.Id,
            RelatedEntityIds: null,
            BeforeState: previousBarcode is not null
                ? $"{{\"barcodeValue\":\"{previousBarcode}\",\"barcodeFormat\":\"{previousFormat}\"}}"
                : null,
            AfterState: $"{{\"barcodeValue\":\"{generatedBarcode}\",\"barcodeFormat\":\"Code128\"}}",
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
    /// Generates a random 12-character Code 128 barcode string using uppercase letters and digits.
    /// </summary>
    private static string GenerateRandomCode128()
    {
        var chars = new char[Code128GeneratedLength];
        for (var i = 0; i < Code128GeneratedLength; i++)
        {
            chars[i] = GenerationCharset[Random.Shared.Next(GenerationCharset.Length)];
        }
        return new string(chars);
    }
}
