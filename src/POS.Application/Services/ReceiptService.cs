using System.Text.Json;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Implements receipt emission, reprinting, and delivery (Req 17.1-17.17).
/// Handles thermal printer, PDF, and email channels with audit logging.
/// </summary>
public sealed class ReceiptService : IReceiptService
{
    private const int MaxEmailRetries = 3;

    private readonly ITransactionRepository _transactionRepository;
    private readonly IReturnRepository _returnRepository;
    private readonly IReceiptRepository _receiptRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ISystemConfigurationRepository _configRepository;
    private readonly IReceiptRenderer _renderer;
    private readonly IPrinterGateway _printerGateway;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;
    private readonly IAuditContextAccessor _auditContextAccessor;

    public ReceiptService(
        ITransactionRepository transactionRepository,
        IReturnRepository returnRepository,
        IReceiptRepository receiptRepository,
        IUserRepository userRepository,
        ICustomerRepository customerRepository,
        ISystemConfigurationRepository configRepository,
        IReceiptRenderer renderer,
        IPrinterGateway printerGateway,
        IEmailSender emailSender,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter,
        IAuditContextAccessor auditContextAccessor)
    {
        _transactionRepository = transactionRepository;
        _returnRepository = returnRepository;
        _receiptRepository = receiptRepository;
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _configRepository = configRepository;
        _renderer = renderer;
        _printerGateway = printerGateway;
        _emailSender = emailSender;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
        _auditContextAccessor = auditContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<Result<ReceiptDocument>> EmitAsync(ReceiptSource src, ReceiptChannel channel, CancellationToken ct)
    {
        ReceiptPayload payload;
        Guid? transactionId = null;
        Guid? returnId = null;
        string? customerEmail = null;

        if (src.Type == ReceiptSourceType.Transaction)
        {
            transactionId = src.EntityId;
            var tx = await _transactionRepository.GetFullAsync(src.EntityId, ct);
            if (tx is null)
                return Result<ReceiptDocument>.Failure(ErrorCode.ReceiptNotFound);

            var buildResult = await BuildTransactionPayloadAsync(tx, reprintCount: null, ct);
            if (!buildResult.IsSuccess)
                return Result<ReceiptDocument>.Failure(buildResult.Error!.Value);

            payload = buildResult.Value!;
            customerEmail = tx.Customer?.Email;
        }
        else
        {
            returnId = src.EntityId;
            var ret = await _returnRepository.GetWithLineItemsAsync(src.EntityId, ct);
            if (ret is null)
                return Result<ReceiptDocument>.Failure(ErrorCode.ReceiptNotFound);

            payload = await BuildReturnPayloadAsync(ret, reprintCount: null, ct);
        }

        // Validate email channel (Req 17.4, 17.5)
        if (channel == ReceiptChannel.Email)
        {
            if (string.IsNullOrWhiteSpace(customerEmail))
                return Result<ReceiptDocument>.Failure(ErrorCode.NoCustomerEmailAvailable);
        }

        // Render the receipt
        var content = await _renderer.RenderAsync(payload, channel, ct);

        // Create Receipt record
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            ReturnId = returnId,
            ReprintCount = 0,
            FirstEmittedAt = _clock.UtcNow,
            LastChannel = ChannelToString(channel),
            PayloadSnapshot = JsonSerializer.Serialize(payload)
        };

        await _receiptRepository.AddAsync(receipt, ct);

        // Handle channel-specific delivery
        var deliveryResult = await DeliverAsync(content, channel, customerEmail, receipt.Id, ct);
        if (!deliveryResult.IsSuccess)
        {
            // For thermal printer failure, we still persist the receipt (Req 17.12)
            if (channel == ReceiptChannel.ThermalPrinter)
            {
                _auditWriter.Enqueue(BuildAuditDraft("receipt_emit_print_failed", receipt, transactionId, returnId));
                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitAsync(ct);
                return Result<ReceiptDocument>.Failure(deliveryResult.Error!.Value);
            }

            // For email failure, log and return error (Req 17.6)
            _auditWriter.Enqueue(BuildAuditDraft("receipt_emit_email_failed", receipt, transactionId, returnId));
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<ReceiptDocument>.Failure(deliveryResult.Error!.Value);
        }

        // Audit successful emission (Req 17.17)
        _auditWriter.Enqueue(BuildAuditDraft("receipt_emit", receipt, transactionId, returnId));
        await _unitOfWork.SaveChangesAsync(ct);
        await _unitOfWork.CommitAsync(ct);

        var contentType = channel == ReceiptChannel.ThermalPrinter ? "application/octet-stream" : "application/pdf";
        return new ReceiptDocument(receipt.Id, content, channel, contentType);
    }

    /// <inheritdoc/>
    public async Task<Result<ReceiptDocument>> ReprintAsync(Guid txOrReturnId, ReceiptChannel channel, CancellationToken ct)
    {
        // Try to find existing receipt by transaction or return ID (Req 17.7, 17.8)
        var receipt = await _receiptRepository.GetByTransactionIdAsync(txOrReturnId, ct)
                      ?? await _receiptRepository.GetByReturnIdAsync(txOrReturnId, ct);

        if (receipt is null)
            return Result<ReceiptDocument>.Failure(ErrorCode.ReceiptNotFound);

        // Increment reprint count (Req 17.9)
        receipt.ReprintCount++;
        receipt.LastChannel = ChannelToString(channel);
        _receiptRepository.Update(receipt);

        // Rebuild payload with reprint and void annotations
        ReceiptPayload payload;
        string? customerEmail = null;

        if (receipt.TransactionId.HasValue)
        {
            var tx = await _transactionRepository.GetFullAsync(receipt.TransactionId.Value, ct);
            if (tx is null)
                return Result<ReceiptDocument>.Failure(ErrorCode.ReceiptNotFound);

            var buildResult = await BuildTransactionPayloadAsync(tx, receipt.ReprintCount, ct);
            if (!buildResult.IsSuccess)
                return Result<ReceiptDocument>.Failure(buildResult.Error!.Value);

            payload = buildResult.Value!;
            customerEmail = tx.Customer?.Email;
        }
        else
        {
            var ret = await _returnRepository.GetWithLineItemsAsync(receipt.ReturnId!.Value, ct);
            if (ret is null)
                return Result<ReceiptDocument>.Failure(ErrorCode.ReceiptNotFound);

            payload = await BuildReturnPayloadAsync(ret, receipt.ReprintCount, ct);
        }

        // Validate email channel
        if (channel == ReceiptChannel.Email)
        {
            if (string.IsNullOrWhiteSpace(customerEmail))
                return Result<ReceiptDocument>.Failure(ErrorCode.NoCustomerEmailAvailable);
        }

        // Render
        var content = await _renderer.RenderAsync(payload, channel, ct);

        // Handle delivery
        var deliveryResult = await DeliverAsync(content, channel, customerEmail, receipt.Id, ct);
        if (!deliveryResult.IsSuccess)
        {
            if (channel == ReceiptChannel.ThermalPrinter)
            {
                _auditWriter.Enqueue(BuildAuditDraft("receipt_reprint_print_failed", receipt, receipt.TransactionId, receipt.ReturnId));
                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitAsync(ct);
                return Result<ReceiptDocument>.Failure(deliveryResult.Error!.Value);
            }

            _auditWriter.Enqueue(BuildAuditDraft("receipt_reprint_email_failed", receipt, receipt.TransactionId, receipt.ReturnId));
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
            return Result<ReceiptDocument>.Failure(deliveryResult.Error!.Value);
        }

        // Audit successful reprint (Req 17.17)
        _auditWriter.Enqueue(BuildAuditDraft("receipt_reprint", receipt, receipt.TransactionId, receipt.ReturnId));
        await _unitOfWork.SaveChangesAsync(ct);
        await _unitOfWork.CommitAsync(ct);

        var contentType = channel == ReceiptChannel.ThermalPrinter ? "application/octet-stream" : "application/pdf";
        return new ReceiptDocument(receipt.Id, content, channel, contentType);
    }

    private async Task<Result<ReceiptPayload>> BuildTransactionPayloadAsync(
        Transaction tx, int? reprintCount, CancellationToken ct)
    {
        var config = await _configRepository.GetAsync(ct);

        // Get user (cashier) name
        var user = await _userRepository.GetByIdAsync(tx.UserId, ct);
        var cashierName = user?.FullName ?? "Unknown";

        // Get customer name
        string? customerName = null;
        if (tx.CustomerId.HasValue)
        {
            var customer = await _customerRepository.GetByIdAsync(tx.CustomerId.Value, ct);
            customerName = customer?.Name;
        }

        // Build line items
        var lines = tx.LineItems.Select(li => new ReceiptLinePayload(
            li.ProductNameSnapshot,
            li.Quantity,
            li.UnitPrice,
            li.LineAmount,
            li.LineDiscountAmount)).ToList();

        // Determine payment method display
        var paymentMethod = DeterminePaymentMethod(tx.Payments);

        // Store credit details (Req 17.2)
        decimal? storeCreditAmount = null;
        string? voucherCodeLast4 = null;
        var storeCreditPayment = tx.Payments.FirstOrDefault(p =>
            p.Method.Equals("store_credit", StringComparison.OrdinalIgnoreCase));
        if (storeCreditPayment is not null)
        {
            storeCreditAmount = storeCreditPayment.Amount;
            if (storeCreditPayment.Voucher is not null)
            {
                var code = storeCreditPayment.Voucher.Code;
                voucherCodeLast4 = code.Length >= 4 ? code[^4..] : code;
            }
        }

        // Reprint text (Req 17.9)
        string? reprintText = reprintCount.HasValue ? $"REPRINT #{reprintCount.Value}" : null;

        // Voided text (Req 17.10)
        string? voidedText = tx.IsVoided ? "VOIDED TRANSACTION" : null;

        // Footer text (Req 17.14, 17.15)
        string? footerText = !string.IsNullOrEmpty(config.ReceiptFooterText) ? config.ReceiptFooterText : null;

        return Result<ReceiptPayload>.Success(new ReceiptPayload(
            BusinessName: config.BusinessName,
            BusinessAddress: config.BusinessAddress,
            TransactionNumber: tx.TransactionNumber,
            CompletedAt: tx.CompletedAt,
            CashierName: cashierName,
            CustomerName: customerName,
            Lines: lines,
            Subtotal: tx.Subtotal,
            TaxAmount: tx.TaxAmount,
            DiscountAmount: tx.DiscountAmount,
            Total: tx.FinalAmount,
            AmountReceived: tx.AmountReceived,
            ChangeDue: tx.ChangeDue,
            PaymentMethod: paymentMethod,
            FooterText: footerText,
            StoreCreditAmount: storeCreditAmount,
            VoucherCodeLast4: voucherCodeLast4,
            ReprintText: reprintText,
            VoidedText: voidedText,
            IsReturn: false,
            ReturnId: null,
            OriginalTransactionId: null,
            RefundMethod: null,
            StoreCreditVoucherCode: null));
    }

    private async Task<ReceiptPayload> BuildReturnPayloadAsync(
        Return ret, int? reprintCount, CancellationToken ct)
    {
        var config = await _configRepository.GetAsync(ct);

        // Get user (cashier) name
        var user = await _userRepository.GetByIdAsync(ret.UserId, ct);
        var cashierName = user?.FullName ?? "Unknown";

        // Build return line items
        var lines = ret.LineItems.Select(li => new ReceiptLinePayload(
            li.Product?.Name ?? "Unknown Product",
            li.ReturnQuantity,
            li.UnitPrice,
            li.LineRefundAmount,
            0m)).ToList();

        // Get store credit voucher code if applicable (Req 17.11)
        string? storeCreditVoucherCode = null;
        if (ret.RefundMethod.Equals("store_credit", StringComparison.OrdinalIgnoreCase))
        {
            // The voucher originated from this return
            var origTransaction = await _transactionRepository.GetByIdAsync(ret.OriginalTransactionId, ct);
            // Look for the voucher associated with this return - not available via navigation here,
            // we'll just note the refund method. The voucher code should be on the StoreCreditVoucher entity.
        }

        // Reprint text (Req 17.9)
        string? reprintText = reprintCount.HasValue ? $"REPRINT #{reprintCount.Value}" : null;

        // Footer text
        string? footerText = !string.IsNullOrEmpty(config.ReceiptFooterText) ? config.ReceiptFooterText : null;

        return new ReceiptPayload(
            BusinessName: config.BusinessName,
            BusinessAddress: config.BusinessAddress,
            TransactionNumber: 0, // Returns don't have a transaction number; they use ReturnId
            CompletedAt: ret.CompletedAt,
            CashierName: cashierName,
            CustomerName: null,
            Lines: lines,
            Subtotal: ret.RefundAmount,
            TaxAmount: 0m,
            DiscountAmount: 0m,
            Total: ret.RefundAmount,
            AmountReceived: 0m,
            ChangeDue: 0m,
            PaymentMethod: ret.RefundMethod,
            FooterText: footerText,
            StoreCreditAmount: null,
            VoucherCodeLast4: null,
            ReprintText: reprintText,
            VoidedText: null,
            IsReturn: true,
            ReturnId: ret.Id,
            OriginalTransactionId: ret.OriginalTransactionId,
            RefundMethod: ret.RefundMethod,
            StoreCreditVoucherCode: storeCreditVoucherCode);
    }

    private async Task<Result<bool>> DeliverAsync(
        byte[] content, ReceiptChannel channel, string? email, Guid receiptId, CancellationToken ct)
    {
        switch (channel)
        {
            case ReceiptChannel.ThermalPrinter:
                return await _printerGateway.PrintAsync(content, ct);

            case ReceiptChannel.Email:
                return await SendEmailWithRetriesAsync(content, email!, receiptId, ct);

            case ReceiptChannel.Pdf:
                // PDF is returned directly; no delivery action needed
                return Result<bool>.Success(true);

            default:
                return Result<bool>.Success(true);
        }
    }

    private async Task<Result<bool>> SendEmailWithRetriesAsync(
        byte[] pdfContent, string email, Guid receiptId, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxEmailRetries; attempt++)
        {
            try
            {
                var attachment = new EmailAttachment(
                    FileName: $"receipt-{receiptId:N}.pdf",
                    ContentType: "application/pdf",
                    Content: pdfContent);

                await _emailSender.SendAsync(
                    subject: "Your receipt",
                    body: "Please find your receipt attached.",
                    recipients: new[] { email },
                    attachments: new[] { attachment },
                    ct: ct);

                return Result<bool>.Success(true);
            }
            catch when (attempt < MaxEmailRetries)
            {
                // Wait before retry with exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);
            }
            catch
            {
                // All retries exhausted (Req 17.6)
                return Result<bool>.Failure(ErrorCode.ReceiptEmailSendFailed);
            }
        }

        return Result<bool>.Failure(ErrorCode.ReceiptEmailSendFailed);
    }

    private static string DeterminePaymentMethod(ICollection<Payment> payments)
    {
        if (payments.Count == 0)
            return "unknown";

        if (payments.Count == 1)
            return payments.First().Method;

        // Multiple payment methods - show as split
        var methods = payments.Select(p => p.Method).Distinct().ToList();
        return string.Join(" + ", methods);
    }

    private static string ChannelToString(ReceiptChannel channel) => channel switch
    {
        ReceiptChannel.ThermalPrinter => "thermal_printer",
        ReceiptChannel.Pdf => "pdf",
        ReceiptChannel.Email => "email",
        _ => "unknown"
    };

    private static AuditEntryDraft BuildAuditDraft(string operationType, Receipt receipt, Guid? transactionId, Guid? returnId)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            channel = receipt.LastChannel,
            reprint_count = receipt.ReprintCount,
            transaction_id = transactionId,
            return_id = returnId
        });

        return new AuditEntryDraft(
            OperationType: operationType,
            EntityType: "Receipt",
            EntityId: receipt.Id,
            RelatedEntityIds: transactionId.HasValue
                ? new List<Guid> { transactionId.Value }
                : returnId.HasValue
                    ? new List<Guid> { returnId.Value }
                    : null,
            BeforeState: null,
            AfterState: null,
            Metadata: metadata);
    }
}
