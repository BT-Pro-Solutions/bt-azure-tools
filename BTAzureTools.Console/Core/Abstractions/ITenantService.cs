using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for listing and selecting Azure tenants.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Lists all tenants the current user has access to.
    /// </summary>
    Task<IReadOnlyList<TenantInfo>> ListTenantsAsync(CancellationToken cancellationToken = default);
}
