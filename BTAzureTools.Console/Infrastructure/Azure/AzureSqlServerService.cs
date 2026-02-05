using Azure.ResourceManager;
using Azure.ResourceManager.Sql;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using AzureCore = Azure.Core;

namespace BTAzureTools.Infrastructure.AzureInfra;

/// <summary>
/// Service for managing Azure SQL Server resources.
/// </summary>
public sealed class AzureSqlServerService : ISqlServerService
{
    private readonly ICredentialProvider _credentialProvider;
    
    public AzureSqlServerService(ICredentialProvider credentialProvider)
    {
        _credentialProvider = credentialProvider;
    }
    
    public async Task<IReadOnlyList<SqlServerInfo>> ListServersAsync(
        SubscriptionInfo subscription,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetTenantCredential(subscription.TenantId);
        var armClient = new ArmClient(credential);
        var subscriptionResource = armClient.GetSubscriptionResource(
            new AzureCore.ResourceIdentifier($"/subscriptions/{subscription.SubscriptionId}"));
        
        var servers = new List<SqlServerInfo>();
        
        await foreach (var server in subscriptionResource.GetSqlServersAsync(cancellationToken: cancellationToken))
        {
            servers.Add(new SqlServerInfo(
                server.Data.Id,
                server.Data.Name,
                GetResourceGroupFromId(server.Data.Id),
                server.Data.FullyQualifiedDomainName ?? $"{server.Data.Name}.database.windows.net",
                subscription.TenantId));
        }
        
        return servers.OrderBy(s => s.Name).ToList();
    }
    
    public async Task<IReadOnlyList<SqlServerInfo>> SearchServersAsync(
        SubscriptionInfo subscription,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var allServers = await ListServersAsync(subscription, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(searchTerm))
            return allServers;
        
        return allServers
            .Where(s => s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       s.FullyQualifiedDomainName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
    
    public async Task<IReadOnlyList<SqlDatabaseInfo>> ListDatabasesAsync(
        SqlServerInfo server,
        CancellationToken cancellationToken = default)
    {
        // Use tenant-scoped credential from the server's tenant
        var credential = _credentialProvider.GetTenantCredential(server.TenantId);
        var armClient = new ArmClient(credential);
        
        var sqlServerResource = armClient.GetSqlServerResource(server.ResourceId);
        var databases = new List<SqlDatabaseInfo>();
        
        await foreach (var db in sqlServerResource.GetSqlDatabases().GetAllAsync(cancellationToken: cancellationToken))
        {
            // Skip system databases
            if (db.Data.Name.Equals("master", StringComparison.OrdinalIgnoreCase))
                continue;
            
            databases.Add(new SqlDatabaseInfo(
                db.Data.Id,
                db.Data.Name,
                server));
        }
        
        return databases.OrderBy(d => d.Name).ToList();
    }
    
    private static string GetResourceGroupFromId(AzureCore.ResourceIdentifier resourceId)
    {
        return resourceId.ResourceGroupName ?? "unknown";
    }
}
