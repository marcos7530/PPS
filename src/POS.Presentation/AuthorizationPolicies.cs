using Microsoft.AspNetCore.Authorization;

namespace POS.Presentation;

/// <summary>
/// Defines authorization policy names and registers them with the DI container.
/// Policies map to the four system roles: Administrator, Manager, Cashier, Viewer.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireAdministrator = nameof(RequireAdministrator);
    public const string RequireManager = nameof(RequireManager);
    public const string RequireCashier = nameof(RequireCashier);
    public const string RequireViewer = nameof(RequireViewer);

    /// <summary>
    /// Registers all role-based authorization policies.
    /// </summary>
    public static AuthorizationOptions AddPosPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireAdministrator, policy =>
            policy.RequireRole("Administrator"));

        options.AddPolicy(RequireManager, policy =>
            policy.RequireRole("Administrator", "Manager"));

        options.AddPolicy(RequireCashier, policy =>
            policy.RequireRole("Administrator", "Manager", "Cashier"));

        options.AddPolicy(RequireViewer, policy =>
            policy.RequireRole("Administrator", "Manager", "Cashier", "Viewer"));

        return options;
    }
}
