using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for discovering resources and assigning Azure RBAC roles at resource scope.
/// </summary>
public interface IResourceIamService
{
    /// <summary>
    /// Lists resources for the given supported resource type in the selected subscription.
    /// </summary>
    Task<IReadOnlyList<ArmResourceInfo>> ListResourcesAsync(
        SubscriptionInfo subscription,
        SupportedResourceType resourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets effective role names assigned at the resource scope for a principal.
    /// </summary>
    Task<IReadOnlyList<string>> GetPrincipalRoleNamesAtScopeAsync(
        SubscriptionInfo subscription,
        string scope,
        Guid principalObjectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns the requested role names at resource scope, skipping roles already assigned.
    /// Returns the role names that were newly created by this call.
    /// </summary>
    Task<IReadOnlyList<string>> AssignRolesAsync(
        SubscriptionInfo subscription,
        string scope,
        PrincipalInfo principal,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default);
}
