using POS.Application.DTOs;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for manager authorization elevation without session change (Req 19.11-19.13, 11.10-11.11).
/// Verifies manager/administrator credentials to authorize a privileged operation
/// while keeping the cashier's session active.
/// </summary>
public interface IElevationService
{
    /// <summary>
    /// Authenticates a manager and issues an elevation grant for the requested permission.
    /// </summary>
    Task<Result<ElevationGrant>> AuthorizeAsync(ElevationRequest req, CancellationToken ct);
}
