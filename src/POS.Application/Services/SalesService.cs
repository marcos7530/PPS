using System.Collections.Concurrent;
using POS.Application.Commands;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Application.Views;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Services;

/// <summary>
/// Manages the full sales transaction lifecycle: open transactions in memory,
/// add line items (by ID or barcode), apply discounts, and complete with payment (Req 9, 18.11-18.16, 19).
/// </summary>
public sealed class SalesService : ISalesService
{
    private const int MinQuantity = 1;
    private const int MaxQuantity = 9999;

    private static readonly HashSet<string> ValidPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash", "credit_card", "debit_card", "store_credit"
    };

    private readonly ConcurrentDictionary<Guid, OpenTransaction> _openTransactions = new();

    private readonly IProductRepository _productRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISystemConfigurationRepository _configRepository;
    private readonly IShiftRepository _shiftRepository;
    private readonly IInventoryReservationGateway _inventoryGateway;
    private readonly IStoreCreditService _storeCreditService;
    private readonly DiscountService _discountService;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public SalesService(
        IProductRepository productRepository,
        ITransactionRepository transactionRepository,
        ISystemConfigurationRepository configRepository,
        IShiftRepository shiftRepository,
        IInventoryReservationGateway inventoryGateway,
        IStoreCreditService storeCreditService,
        DiscountService discountService,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _productRepository = productRepository;
        _transactionRepository = transactionRepository;
        _configRepository = configRepository;
        _shiftRepository = shiftRepository;
        _inventoryGateway = inventoryGateway;
        _storeCreditService = storeCreditService;
        _discountService = discountService;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Adds a line item to an open transaction by product ID.
    /// If the product already exists in the transaction, increments quantity.
    /// Validates product active, stock availability, and quantity limits (Req 9.1-9.5).
    /// </summary>
    public async Task<Result<OpenTransactionView>> AddLineItemAsync(
        Guid txId, Guid productId, int qty, CancellationToken ct)
    {
        if (qty < MinQuantity || qty > MaxQuantity)
            return Result<OpenTransactionView>.Failure(ErrorCode.LineItemQuantityExceedsMaximum);

        var product = await _productRepository.GetByIdAsync(productId, ct);
        if (product is null)
            return Result<OpenTransactionView>.Failure(ErrorCode.InvalidProductIdentifier);

        if (product.IsDeactivated)
            return Result<OpenTransactionView>.Failure(ErrorCode.ProductNoLongerAvailable);

        var openTx = _openTransactions.GetOrAdd(txId, _ => new OpenTransaction(txId));

        lock (openTx.Lock)
        {
            // Check if product already in transaction
            var existing = openTx.LineItems.FirstOrDefault(li => li.ProductId == productId);
            if (existing is not null)
            {
                var newQty = existing.Quantity + qty;
                if (newQty > MaxQuantity)
                    return Result<OpenTransactionView>.Failure(ErrorCode.LineItemQuantityExceedsMaximum);

                // Validate stock (Req 9.5)
                if (newQty > product.Quantity)
                    return Result<OpenTransactionView>.Failure(ErrorCode.InsufficientInventory);

                existing.Quantity = newQty;
                existing.LineAmount = CalculateLineAmount(existing);
            }
            else
            {
                // Validate stock (Req 9.5)
                if (qty > product.Quantity)
                    return Result<OpenTransactionView>.Failure(ErrorCode.InsufficientInventory);

                var lineItem = new OpenLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Quantity = qty,
                    UnitPrice = product.SalePrice,
                    CostPrice = product.CostPrice,
                    LineDiscountAmount = 0m,
                    LineAmount = Math.Round(product.SalePrice * qty, 2, MidpointRounding.AwayFromZero)
                };
                openTx.LineItems.Add(lineItem);
            }

            return Result<OpenTransactionView>.Success(BuildView(openTx));
        }
    }

    /// <summary>
    /// Adds a line item by barcode scan. New product: qty=1; existing: qty+1 (Req 18.11-18.16).
    /// </summary>
    public async Task<Result<OpenTransactionView>> AddByBarcodeAsync(
        Guid txId, string barcode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return Result<OpenTransactionView>.Failure(ErrorCode.BarcodeNotFound);

        var product = await _productRepository.GetByBarcodeAsync(barcode, ct);
        if (product is null)
            return Result<OpenTransactionView>.Failure(ErrorCode.BarcodeNotFound);

        if (product.IsDeactivated)
            return Result<OpenTransactionView>.Failure(ErrorCode.ProductNoLongerAvailable);

        var openTx = _openTransactions.GetOrAdd(txId, _ => new OpenTransaction(txId));

        lock (openTx.Lock)
        {
            var existing = openTx.LineItems.FirstOrDefault(li => li.ProductId == product.Id);
            if (existing is not null)
            {
                // Existing item: increment by 1
                var newQty = existing.Quantity + 1;
                if (newQty > MaxQuantity)
                    return Result<OpenTransactionView>.Failure(ErrorCode.LineItemQuantityExceedsMaximum);

                if (newQty > product.Quantity)
                    return Result<OpenTransactionView>.Failure(ErrorCode.InsufficientInventory);

                existing.Quantity = newQty;
                existing.LineAmount = CalculateLineAmount(existing);
            }
            else
            {
                // New item: qty = 1
                if (product.Quantity < 1)
                    return Result<OpenTransactionView>.Failure(ErrorCode.InsufficientInventory);

                var lineItem = new OpenLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Quantity = 1,
                    UnitPrice = product.SalePrice,
                    CostPrice = product.CostPrice,
                    LineDiscountAmount = 0m,
                    LineAmount = Math.Round(product.SalePrice, 2, MidpointRounding.AwayFromZero)
                };
                openTx.LineItems.Add(lineItem);
            }

            return Result<OpenTransactionView>.Success(BuildView(openTx));
        }
    }

    /// <summary>
    /// Applies a percentage discount to a line item via the DiscountService (Req 19).
    /// </summary>
    public async Task<Result<OpenTransactionView>> ApplyLineDiscountAsync(
        ApplyDiscountCommand cmd, CancellationToken ct)
    {
        if (!_openTransactions.TryGetValue(cmd.TransactionId, out var openTx))
            return Result<OpenTransactionView>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        lock (openTx.Lock)
        {
            var lineItem = openTx.LineItems.FirstOrDefault(li => li.Id == cmd.LineItemId);
            if (lineItem is null)
                return Result<OpenTransactionView>.Failure(ErrorCode.InvalidProductIdentifier);

            // We need to release the lock for async operations - capture the needed data
            var lineAmount = lineItem.LineAmount + lineItem.LineDiscountAmount; // gross line amount
            var costPrice = lineItem.CostPrice;
            var quantity = lineItem.Quantity;
            var lineItemId = lineItem.Id;
        }

        // Apply discount asynchronously outside the lock
        OpenLineItem? targetLine;
        decimal grossLineAmount;

        lock (openTx.Lock)
        {
            targetLine = openTx.LineItems.FirstOrDefault(li => li.Id == cmd.LineItemId);
            if (targetLine is null)
                return Result<OpenTransactionView>.Failure(ErrorCode.InvalidProductIdentifier);
            grossLineAmount = targetLine.UnitPrice * targetLine.Quantity;
        }

        ElevationRequest? elevation = cmd.AuthorizedBy.HasValue
            ? new ElevationRequest(string.Empty, string.Empty, "apply_discount", cmd.AuthorizedBy.Value)
            : null;

        var discountResult = await _discountService.ApplyLineItemDiscountAsync(
            cmd.LineItemId,
            "percentage",
            cmd.DiscountPercentage,
            null,
            cmd.Reason,
            null,
            cmd.AuthorizedBy ?? Guid.Empty, // The caller's userId; use AuthorizedBy for now
            grossLineAmount,
            targetLine.CostPrice,
            targetLine.Quantity,
            elevation,
            ct);

        if (!discountResult.IsSuccess)
            return Result<OpenTransactionView>.Failure(discountResult.Error!.Value);

        var discount = discountResult.Value!.LineDiscount!;

        lock (openTx.Lock)
        {
            var line = openTx.LineItems.FirstOrDefault(li => li.Id == cmd.LineItemId);
            if (line is null)
                return Result<OpenTransactionView>.Failure(ErrorCode.InvalidProductIdentifier);

            line.LineDiscountAmount = discount.Amount;
            line.LineAmount = CalculateLineAmount(line);
            line.Discount = discount;

            return Result<OpenTransactionView>.Success(BuildView(openTx));
        }
    }

    /// <summary>
    /// Completes the transaction: validates payments, adjusts inventory atomically,
    /// persists the transaction with all details (Req 9.6-9.22).
    /// </summary>
    public async Task<Result<CompletedSale>> CompleteAsync(CompleteSaleCommand cmd, CancellationToken ct)
    {
        if (!_openTransactions.TryGetValue(cmd.TransactionId, out var openTx))
            return Result<CompletedSale>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        List<OpenLineItem> lineItems;
        lock (openTx.Lock)
        {
            if (openTx.LineItems.Count == 0)
                return Result<CompletedSale>.Failure(ErrorCode.InvalidOrExpiredTransaction);

            lineItems = openTx.LineItems.ToList();
        }

        var config = await _configRepository.GetAsync(ct);
        var now = _clock.UtcNow;

        // Calculate totals with 2-decimal precision (Req 9.3)
        var subtotal = new Money(lineItems.Sum(li => li.UnitPrice * li.Quantity));
        var totalLineDiscounts = new Money(lineItems.Sum(li => li.LineDiscountAmount));
        var discountAmount = totalLineDiscounts;

        // Add transaction-level discount if present
        lock (openTx.Lock)
        {
            if (openTx.TransactionDiscount is not null)
            {
                discountAmount = new Money(discountAmount.Amount + openTx.TransactionDiscount.Amount);
            }
        }

        var taxRate = config.TaxRatePercentage / 100m;
        var taxableAmount = new Money(subtotal.Amount - discountAmount.Amount);
        var taxAmount = new Money(taxableAmount.Amount * taxRate);
        var finalAmount = new Money(taxableAmount.Amount + taxAmount.Amount);

        // Validate payment methods
        if (cmd.Payments is null || cmd.Payments.Count == 0)
            return Result<CompletedSale>.Failure(ErrorCode.InsufficientPayment);

        foreach (var payment in cmd.Payments)
        {
            if (!ValidPaymentMethods.Contains(payment.Method))
                return Result<CompletedSale>.Failure(ErrorCode.InsufficientPayment);

            if (payment.Amount <= 0)
                return Result<CompletedSale>.Failure(ErrorCode.InsufficientPayment);
        }

        // Req 9.7: Cash payment requires active shift
        var hasCashPayment = cmd.Payments.Any(p =>
            p.Method.Equals("cash", StringComparison.OrdinalIgnoreCase));

        Shift? activeShift = null;
        if (hasCashPayment)
        {
            activeShift = await _shiftRepository.GetActiveByUserIdAsync(cmd.UserId, ct);
            if (activeShift is null)
                return Result<CompletedSale>.Failure(ErrorCode.NoActiveShiftForCashTransaction);
        }
        else
        {
            // Req 9.20: Card payment without active shift: record with null shift_id
            activeShift = await _shiftRepository.GetActiveByUserIdAsync(cmd.UserId, ct);
        }

        // Process store credit payments first
        var payments = new List<Payment>();
        decimal totalStoreCreditApplied = 0m;

        foreach (var paymentDetail in cmd.Payments.Where(p =>
            p.Method.Equals("store_credit", StringComparison.OrdinalIgnoreCase)))
        {
            var remainingToApply = new Money(Math.Min(paymentDetail.Amount, finalAmount.Amount - totalStoreCreditApplied));

            var storeCreditReq = new StoreCreditRequest(
                cmd.TransactionId,
                cmd.CustomerId ?? Guid.Empty,
                paymentDetail.VoucherCode);

            var scResult = await _storeCreditService.ConsumeAsync(storeCreditReq, remainingToApply, ct);
            if (!scResult.IsSuccess)
                return Result<CompletedSale>.Failure(scResult.Error!.Value);

            var applied = scResult.Value!;
            totalStoreCreditApplied += applied.AmountApplied.Amount;

            var scPayment = new Payment
            {
                Id = Guid.NewGuid(),
                TransactionId = cmd.TransactionId,
                Method = "store_credit",
                Amount = applied.AmountApplied.Amount,
                VoucherId = !string.IsNullOrWhiteSpace(paymentDetail.VoucherCode) ? null : null,
                StoreCreditCustomerId = string.IsNullOrWhiteSpace(paymentDetail.VoucherCode)
                    ? cmd.CustomerId
                    : null,
                IsConsumptionActive = true,
                CreatedAt = now
            };
            payments.Add(scPayment);
        }

        // Process non-store-credit payments
        foreach (var paymentDetail in cmd.Payments.Where(p =>
            !p.Method.Equals("store_credit", StringComparison.OrdinalIgnoreCase)))
        {
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                TransactionId = cmd.TransactionId,
                Method = paymentDetail.Method.ToLowerInvariant(),
                Amount = paymentDetail.Amount,
                IsConsumptionActive = true,
                CreatedAt = now
            };
            payments.Add(payment);
        }

        // Validate total payment covers final amount (Req 9.6)
        var totalReceived = new Money(payments.Sum(p => p.Amount));
        if (totalReceived < finalAmount)
            return Result<CompletedSale>.Failure(ErrorCode.InsufficientPayment);

        var changeDue = new Money(totalReceived.Amount - finalAmount.Amount);

        // Derive operating day from business timezone
        var operatingDay = DeriveOperatingDay(now, config.BusinessTimeZone);

        // Begin atomic transaction
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Atomically decrement inventory (Req 9.21-9.22)
            var stockDeltas = lineItems
                .Select(li => new StockDelta(li.ProductId, -li.Quantity))
                .ToList();

            var inventoryResult = await _inventoryGateway.LockAndAdjustAsync(stockDeltas, ct);
            if (!inventoryResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<CompletedSale>.Failure(ErrorCode.TransactionInventoryUpdateFailed);
            }

            // Get next transaction number
            var transactionNumber = await _transactionRepository.GetNextTransactionNumberAsync(ct);

            // Build and persist transaction entity
            var transaction = new Transaction
            {
                Id = cmd.TransactionId,
                TransactionNumber = transactionNumber,
                CompletedAt = now,
                OperatingDay = operatingDay,
                UserId = cmd.UserId,
                ShiftId = activeShift?.Id,
                CustomerId = cmd.CustomerId,
                Subtotal = subtotal.Amount,
                TaxAmount = taxAmount.Amount,
                DiscountAmount = discountAmount.Amount,
                FinalAmount = finalAmount.Amount,
                AmountReceived = totalReceived.Amount,
                ChangeDue = changeDue.Amount,
                TaxRateApplied = config.TaxRatePercentage,
                IsVoided = false,
                LineItems = BuildTransactionLineItems(lineItems, cmd.TransactionId),
                Payments = payments
            };

            // Add transaction-level discount if present
            lock (openTx.Lock)
            {
                if (openTx.TransactionDiscount is not null)
                {
                    transaction.TransactionDiscount = openTx.TransactionDiscount;
                }
            }

            await _transactionRepository.AddAsync(transaction, ct);

            // Audit the completed sale (Req 1.7)
            var lineItemsJson = string.Join(",", lineItems.Select(li =>
                $"{{\"productId\":\"{li.ProductId}\",\"qty\":{li.Quantity},\"unitPrice\":{li.UnitPrice},\"discount\":{li.LineDiscountAmount}}}"));

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "CompleteSale",
                EntityType: "Transaction",
                EntityId: transaction.Id,
                RelatedEntityIds: lineItems.Select(li => li.ProductId).Distinct().ToList(),
                BeforeState: null,
                AfterState: $"{{\"transactionNumber\":{transactionNumber},\"subtotal\":{subtotal.Amount},\"taxAmount\":{taxAmount.Amount},\"discountAmount\":{discountAmount.Amount},\"finalAmount\":{finalAmount.Amount},\"amountReceived\":{totalReceived.Amount},\"changeDue\":{changeDue.Amount},\"paymentMethods\":[{string.Join(",", payments.Select(p => $"\"{p.Method}\""))}],\"lineItems\":[{lineItemsJson}]}}",
                Metadata: activeShift is not null
                    ? $"{{\"shiftId\":\"{activeShift.Id}\",\"operatingDay\":\"{operatingDay}\"}}"
                    : $"{{\"operatingDay\":\"{operatingDay}\"}}"));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        // Remove from open transactions
        _openTransactions.TryRemove(cmd.TransactionId, out _);

        return Result<CompletedSale>.Success(new CompletedSale(
            cmd.TransactionId,
            await _transactionRepository.GetNextTransactionNumberAsync(ct) - 1, // We just used this number
            finalAmount,
            totalReceived,
            changeDue,
            now));
    }

    #region Private Helpers

    private static decimal CalculateLineAmount(OpenLineItem item)
    {
        var gross = Math.Round(item.UnitPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);
        return Math.Round(gross - item.LineDiscountAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static OpenTransactionView BuildView(OpenTransaction openTx)
    {
        var lineViews = openTx.LineItems.Select(li => new LineItemView(
            li.Id,
            li.ProductId,
            li.ProductName,
            li.Sku,
            li.Quantity,
            new Money(li.UnitPrice),
            new Money(li.LineAmount),
            new Money(li.LineDiscountAmount)
        )).ToList();

        var subtotal = new Money(openTx.LineItems.Sum(li => li.UnitPrice * li.Quantity));
        var totalDiscount = new Money(openTx.LineItems.Sum(li => li.LineDiscountAmount)
            + (openTx.TransactionDiscount?.Amount ?? 0m));

        // Tax is computed but we don't have config here in the lock; approximate with 0 for view
        // The real tax is computed at completion time
        var taxAmount = Money.Zero;
        var total = new Money(subtotal.Amount - totalDiscount.Amount + taxAmount.Amount);

        return new OpenTransactionView(
            openTx.TransactionId,
            lineViews,
            subtotal,
            taxAmount,
            totalDiscount,
            total);
    }

    private static List<TransactionLineItem> BuildTransactionLineItems(
        List<OpenLineItem> openItems, Guid transactionId)
    {
        return openItems.Select(li => new TransactionLineItem
        {
            Id = li.Id,
            TransactionId = transactionId,
            ProductId = li.ProductId,
            ProductNameSnapshot = li.ProductName,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice,
            RecordedCostPrice = li.CostPrice,
            LineDiscountAmount = li.LineDiscountAmount,
            LineAmount = li.LineAmount,
            ReturnedQuantity = 0,
            Discount = li.Discount
        }).ToList();
    }

    private static DateOnly DeriveOperatingDay(DateTimeOffset utcNow, string businessTimeZone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(businessTimeZone);
            var localTime = TimeZoneInfo.ConvertTime(utcNow, tz);
            return DateOnly.FromDateTime(localTime.DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback: use UTC date
            return DateOnly.FromDateTime(utcNow.UtcDateTime);
        }
    }

    #endregion

    #region Internal Types

    /// <summary>
    /// In-memory representation of an open (not yet completed) transaction.
    /// </summary>
    internal sealed class OpenTransaction
    {
        public Guid TransactionId { get; }
        public List<OpenLineItem> LineItems { get; } = new();
        public TransactionDiscount? TransactionDiscount { get; set; }
        public object Lock { get; } = new();

        public OpenTransaction(Guid transactionId)
        {
            TransactionId = transactionId;
        }
    }

    /// <summary>
    /// In-memory representation of a line item in an open transaction.
    /// </summary>
    internal sealed class OpenLineItem
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal LineDiscountAmount { get; set; }
        public decimal LineAmount { get; set; }
        public LineItemDiscount? Discount { get; set; }
    }

    #endregion
}
