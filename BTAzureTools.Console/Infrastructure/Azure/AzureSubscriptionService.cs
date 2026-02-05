using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;

namespace BTAzureTools.Infrastructure.AzureInfra;

/// <summary>
/// Service for listing Azure subscriptions.
/// </summary>
public sealed class AzureSubscriptionService : ISubscriptionService
{
    private readonly ICredentialProvider _credentialProvider;
    
    public AzureSubscriptionService(ICredentialProvider credentialProvider)
    {
        _credentialProvider = credentialProvider;
    }
    
    public async Task<IReadOnlyList<SubscriptionInfo>> ListSubscriptionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetTenantCredential(tenantId);
        var armClient = new ArmClient(credential);
        
        var subscriptions = new List<SubscriptionInfo>();
        
        await foreach (var sub in armClient.GetSubscriptions().GetAllAsync(cancellationToken))
        {
            subscriptions.Add(new SubscriptionInfo(
                sub.Data.SubscriptionId,
                sub.Data.DisplayName,
                tenantId));
        }
        
        return subscriptions.OrderBy(s => s.DisplayName).ToList();
    }
}
