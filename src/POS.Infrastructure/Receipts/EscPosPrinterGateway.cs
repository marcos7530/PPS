using POS.Application.Interfaces.Infrastructure;
using POS.Domain.Common;

namespace POS.Infrastructure.Receipts;

/// <summary>
/// Sends rendered receipt data to a local ESC/POS thermal printer agent (Req 17.3, 17.12).
/// POSTs to http://localhost:9100/print with a 5-second timeout.
/// On failure returns ReceiptPrintFailed; the transaction/return is preserved.
/// </summary>
public sealed class EscPosPrinterGateway : IPrinterGateway
{
    private readonly HttpClient _httpClient;

    public EscPosPrinterGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri("http://localhost:9100");
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> PrintAsync(byte[] payload, CancellationToken ct)
    {
        try
        {
            using var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync("/print", content, ct);

            if (response.IsSuccessStatusCode)
                return Result<bool>.Success(true);

            return Result<bool>.Failure(ErrorCode.ReceiptPrintFailed);
        }
        catch (TaskCanceledException)
        {
            // Timeout
            return Result<bool>.Failure(ErrorCode.ReceiptPrintFailed);
        }
        catch (HttpRequestException)
        {
            // Connection refused or network error
            return Result<bool>.Failure(ErrorCode.ReceiptPrintFailed);
        }
    }
}
