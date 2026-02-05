using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for listing Azure subscriptions.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Lists all subscriptions in the specified tenant.
    /// </summary>
    Task<IReadOnlyList<SubscriptionInfo>> ListSubscriptionsAsync(
        Guid tenantId, 
        CancellationToken cancellationToken = default);
}
