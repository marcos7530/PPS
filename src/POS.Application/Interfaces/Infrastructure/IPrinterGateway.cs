using POS.Domain.Common;

namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Port for sending receipt data to a thermal printer via local agent (Req 17.3, 17.12).
/// </summary>
public interface IPrinterGateway
{
    /// <summary>
    /// Sends the rendered byte payload to the thermal printer.
    /// Returns failure with ReceiptPrintFailed if the printer agent is unreachable or times out.
    /// </summary>
    Task<Result<bool>> PrintAsync(byte[] payload, CancellationToken ct);
}
