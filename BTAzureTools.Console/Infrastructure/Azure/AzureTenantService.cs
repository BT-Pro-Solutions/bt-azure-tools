using Azure.ResourceManager;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;

namespace BTAzureTools.Infrastructure.AzureInfra;

/// <summary>
/// Service for listing Azure tenants.
/// </summary>
public sealed class AzureTenantService : ITenantService
{
    private readonly ICredentialProvider _credentialProvider;
    
    public AzureTenantService(ICredentialProvider credentialProvider)
    {
        _credentialProvider = credentialProvider;
    }
    
    public async Task<IReadOnlyList<TenantInfo>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetBootstrapCredential();
        var armClient = new ArmClient(credential);
        
        var tenants = new List<TenantInfo>();
        
        await foreach (var tenant in armClient.GetTenants().GetAllAsync(cancellationToken))
        {
            if (tenant.Data.TenantId.HasValue)
            {
                tenants.Add(new TenantInfo(
                    tenant.Data.TenantId.Value,
                    tenant.Data.DisplayName ?? tenant.Data.TenantId.Value.ToString(),
                    tenant.Data.DefaultDomain));
            }
        }
        
        return tenants;
    }
}
