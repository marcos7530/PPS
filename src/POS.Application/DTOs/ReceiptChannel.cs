namespace POS.Application.DTOs;

/// <summary>
/// Channel through which a receipt is emitted.
/// </summary>
public enum ReceiptChannel
{
    ThermalPrinter,
    Pdf,
    Email
}
