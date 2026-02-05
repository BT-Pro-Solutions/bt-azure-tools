using Azure.Core;
using Azure.Identity;
using BTAzureTools.Core.Abstractions;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace BTAzureTools.Infrastructure.AzureInfra;

/// <summary>
/// Provides Azure credentials using a chained credential approach:
/// 1. Azure CLI credential (uses existing 'az login' session)
/// 2. Interactive browser (fallback when CLI not available)
/// </summary>
public sealed class AzureCredentialProvider : ICredentialProvider
{
    private readonly Dictionary<Guid, TokenCredential> _tenantCredentials = new();
    private TokenCredential? _bootstrapCredential;
    
    public TokenCredential GetBootstrapCredential()
    {
        _bootstrapCredential ??= CreateChainedCredential(tenantId: null);
        return _bootstrapCredential;
    }
    
    public TokenCredential GetTenantCredential(Guid tenantId)
    {
        if (!_tenantCredentials.TryGetValue(tenantId, out var credential))
        {
            credential = CreateChainedCredential(tenantId.ToString());
            _tenantCredentials[tenantId] = credential;
        }
        return credential;
    }
    
    public async Task<(Guid ObjectId, string DisplayName, string? UserPrincipalName)> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var credential = GetBootstrapCredential();
        var graphClient = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        
        var me = await graphClient.Me.GetAsync(rc =>
        {
            rc.QueryParameters.Select = ["id", "displayName", "userPrincipalName"];
        }, cancellationToken);
        
        if (me?.Id is null)
            throw new InvalidOperationException("Unable to retrieve current user information.");
        
        return (Guid.Parse(me.Id), me.DisplayName ?? "Unknown", me.UserPrincipalName);
    }
    
    /// <summary>
    /// Creates a chained credential that tries Azure CLI first, then falls back to interactive browser.
    /// </summary>
    private static TokenCredential CreateChainedCredential(string? tenantId)
    {
        var cliCredential = new AzureCliCredential(new AzureCliCredentialOptions
        {
            TenantId = tenantId
        });
        
        var interactiveCredential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
        {
            TenantId = tenantId,
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "BTAzureTools" }
        });
        
        return new ChainedTokenCredential(cliCredential, interactiveCredential);
    }
}
