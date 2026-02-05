using Azure.Core;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Provides Azure credentials for authentication.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>
    /// Gets a credential for initial authentication (tenant discovery).
    /// </summary>
    TokenCredential GetBootstrapCredential();
    
    /// <summary>
    /// Gets a credential scoped to a specific tenant.
    /// </summary>
    TokenCredential GetTenantCredential(Guid tenantId);
    
    /// <summary>
    /// Gets the currently authenticated user's object ID and display name.
    /// </summary>
    Task<(Guid ObjectId, string DisplayName, string? UserPrincipalName)> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
