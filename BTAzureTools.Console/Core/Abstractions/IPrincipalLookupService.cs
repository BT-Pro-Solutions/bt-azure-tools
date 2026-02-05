using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for looking up Entra ID principals.
/// </summary>
public interface IPrincipalLookupService
{
    /// <summary>
    /// Searches for users by email or display name.
    /// </summary>
    Task<IReadOnlyList<PrincipalInfo>> SearchUsersAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds a user by their email/UPN.
    /// </summary>
    Task<PrincipalInfo?> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for service principals (apps and managed identities).
    /// </summary>
    Task<IReadOnlyList<PrincipalInfo>> SearchServicePrincipalsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Finds a service principal by object ID, client ID, or display name.
    /// </summary>
    Task<PrincipalInfo?> FindServicePrincipalAsync(
        string identifier,
        CancellationToken cancellationToken = default);
}
