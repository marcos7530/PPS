using System.Globalization;
using POS.Application.DTOs;
using POS.Application.Interfaces.Infrastructure;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace POS.Infrastructure.Receipts;

/// <summary>
/// Renders receipt content as PDF using QuestPDF (Req 17.3).
/// Generates 80mm-width page PDFs for both thermal printer and downloadable formats.
/// </summary>
public sealed class QuestPdfReceiptRenderer : IReceiptRenderer
{
    private const float PageWidthMm = 80f;
    private const float MarginMm = 3f;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public Task<byte[]> RenderAsync(ReceiptPayload payload, ReceiptChannel channel, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(PageWidthMm, Unit.Millimetre);
                page.MarginHorizontal(MarginMm, Unit.Millimetre);
                page.MarginVertical(2f, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Content().Column(column =>
                {
                    column.Spacing(2);

                    // Header: Business info
                    RenderHeader(column, payload);

                    // Reprint / Voided annotations
                    RenderAnnotations(column, payload);

                    // Transaction / Return info
                    RenderTransactionInfo(column, payload);

                    // Line items
                    RenderLineItems(column, payload);

                    // Totals
                    RenderTotals(column, payload);

                    // Payment info
                    RenderPaymentInfo(column, payload);

                    // Store credit details
                    RenderStoreCreditDetails(column, payload);

                    // Footer
                    RenderFooter(column, payload);
                });
            });
        });

        var bytes = document.GeneratePdf();
        return Task.FromResult(bytes);
    }

    private static void RenderHeader(ColumnDescriptor column, ReceiptPayload payload)
    {
        column.Item().AlignCenter().Text(payload.BusinessName)
            .Bold().FontSize(10);

        column.Item().AlignCenter().Text(payload.BusinessAddress)
            .FontSize(7);

        column.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    private static void RenderAnnotations(ColumnDescriptor column, ReceiptPayload payload)
    {
        if (!string.IsNullOrEmpty(payload.VoidedText))
        {
            column.Item().AlignCenter().Text(payload.VoidedText)
                .Bold().FontSize(10);
            column.Item().PaddingVertical(1).LineHorizontal(0.5f);
        }

        if (!string.IsNullOrEmpty(payload.ReprintText))
        {
            column.Item().AlignCenter().Text(payload.ReprintText)
                .Bold().FontSize(9);
            column.Item().PaddingVertical(1).LineHorizontal(0.5f);
        }
    }

    private static void RenderTransactionInfo(ColumnDescriptor column, ReceiptPayload payload)
    {
        if (payload.IsReturn)
        {
            column.Item().Text("RETURN RECEIPT").Bold().FontSize(9);
            column.Item().Text(text =>
            {
                text.Span("Return ID: ").FontSize(7);
                text.Span(payload.ReturnId?.ToString("N", Inv) ?? string.Empty).FontSize(7);
            });
            column.Item().Text(text =>
            {
                text.Span("Original TX: ").FontSize(7);
                text.Span(payload.OriginalTransactionId?.ToString("N", Inv) ?? string.Empty).FontSize(7);
            });
        }
        else
        {
            column.Item().Text(string.Create(Inv, $"TX #: {payload.TransactionNumber}"));
        }

        column.Item().Text(string.Create(Inv, $"Date: {payload.CompletedAt:yyyy-MM-dd HH:mm:ss}"));
        column.Item().Text(string.Create(Inv, $"Cashier: {payload.CashierName}"));

        if (!string.IsNullOrEmpty(payload.CustomerName))
        {
            column.Item().Text(string.Create(Inv, $"Customer: {payload.CustomerName}"));
        }

        column.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    private static void RenderLineItems(ColumnDescriptor column, ReceiptPayload payload)
    {
        // Header row
        column.Item().Row(row =>
        {
            row.RelativeItem(4).Text("Item").Bold().FontSize(7);
            row.RelativeItem(1).AlignRight().Text("Qty").Bold().FontSize(7);
            row.RelativeItem(2).AlignRight().Text("Price").Bold().FontSize(7);
            row.RelativeItem(2).AlignRight().Text("Total").Bold().FontSize(7);
        });

        foreach (var line in payload.Lines)
        {
            column.Item().Row(row =>
            {
                row.RelativeItem(4).Text(line.ProductName).FontSize(7);
                row.RelativeItem(1).AlignRight().Text(line.Quantity.ToString(Inv)).FontSize(7);
                row.RelativeItem(2).AlignRight().Text(line.UnitPrice.ToString("F2", Inv)).FontSize(7);
                row.RelativeItem(2).AlignRight().Text(line.LineTotal.ToString("F2", Inv)).FontSize(7);
            });

            if (line.DiscountAmount > 0)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem(7).Text(string.Create(Inv, $"  Discount: -{line.DiscountAmount:F2}")).FontSize(6).Italic();
                    row.RelativeItem(2).AlignRight().Text(string.Empty);
                });
            }
        }

        column.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    private static void RenderTotals(ColumnDescriptor column, ReceiptPayload payload)
    {
        if (!payload.IsReturn)
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Subtotal:");
                row.RelativeItem().AlignRight().Text(payload.Subtotal.ToString("F2", Inv));
            });

            if (payload.TaxAmount != 0)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Tax:");
                    row.RelativeItem().AlignRight().Text(payload.TaxAmount.ToString("F2", Inv));
                });
            }

            if (payload.DiscountAmount != 0)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Discount:");
                    row.RelativeItem().AlignRight().Text(string.Create(Inv, $"-{payload.DiscountAmount:F2}"));
                });
            }

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("TOTAL:").Bold();
                row.RelativeItem().AlignRight().Text(payload.Total.ToString("F2", Inv)).Bold();
            });
        }
        else
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("REFUND TOTAL:").Bold();
                row.RelativeItem().AlignRight().Text(payload.Total.ToString("F2", Inv)).Bold();
            });

            if (!string.IsNullOrEmpty(payload.RefundMethod))
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text("Refund method:");
                    row.RelativeItem().AlignRight().Text(payload.RefundMethod);
                });
            }
        }

        column.Item().PaddingVertical(2).LineHorizontal(0.5f);
    }

    private static void RenderPaymentInfo(ColumnDescriptor column, ReceiptPayload payload)
    {
        if (payload.IsReturn)
            return;

        column.Item().Row(row =>
        {
            row.RelativeItem().Text("Payment:");
            row.RelativeItem().AlignRight().Text(payload.PaymentMethod);
        });

        column.Item().Row(row =>
        {
            row.RelativeItem().Text("Received:");
            row.RelativeItem().AlignRight().Text(payload.AmountReceived.ToString("F2", Inv));
        });

        if (payload.ChangeDue > 0)
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Change:");
                row.RelativeItem().AlignRight().Text(payload.ChangeDue.ToString("F2", Inv));
            });
        }
    }

    private static void RenderStoreCreditDetails(ColumnDescriptor column, ReceiptPayload payload)
    {
        if (payload.StoreCreditAmount.HasValue && payload.StoreCreditAmount.Value > 0)
        {
            column.Item().PaddingVertical(1).LineHorizontal(0.5f);
            column.Item().Row(row =>
            {
                row.RelativeItem().Text("Store Credit Applied:");
                row.RelativeItem().AlignRight().Text(payload.StoreCreditAmount.Value.ToString("F2", Inv));
            });

            if (!string.IsNullOrEmpty(payload.VoucherCodeLast4))
            {
                column.Item().Text(string.Create(Inv, $"Voucher: ****{payload.VoucherCodeLast4}")).FontSize(7);
            }
        }

        // Return receipt: show store credit voucher code
        if (payload.IsReturn && !string.IsNullOrEmpty(payload.StoreCreditVoucherCode))
        {
            column.Item().PaddingVertical(1).LineHorizontal(0.5f);
            column.Item().Text(string.Create(Inv, $"Store Credit Voucher: {payload.StoreCreditVoucherCode}")).FontSize(7);
        }
    }

    private static void RenderFooter(ColumnDescriptor column, ReceiptPayload payload)
    {
        if (!string.IsNullOrEmpty(payload.FooterText))
        {
            column.Item().PaddingVertical(3).LineHorizontal(0.5f);
            column.Item().AlignCenter().Text(payload.FooterText).FontSize(7);
        }
    }
}
