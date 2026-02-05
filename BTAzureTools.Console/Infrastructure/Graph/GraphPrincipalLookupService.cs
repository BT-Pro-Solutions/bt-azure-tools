using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace BTAzureTools.Infrastructure.Graph;

/// <summary>
/// Service for looking up Entra ID principals using Microsoft Graph.
/// </summary>
public sealed class GraphPrincipalLookupService : IPrincipalLookupService
{
    private readonly ICredentialProvider _credentialProvider;
    private GraphServiceClient? _graphClient;
    
    public GraphPrincipalLookupService(ICredentialProvider credentialProvider)
    {
        _credentialProvider = credentialProvider;
    }
    
    private GraphServiceClient GetGraphClient()
    {
        return _graphClient ??= new GraphServiceClient(
            _credentialProvider.GetBootstrapCredential(),
            ["https://graph.microsoft.com/.default"]);
    }
    
    public async Task<IReadOnlyList<PrincipalInfo>> SearchUsersAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];
        
        var client = GetGraphClient();
        
        // Search by displayName or mail
        var users = await client.Users.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = $"startswith(displayName,'{EscapeOData(searchTerm)}') or startswith(mail,'{EscapeOData(searchTerm)}') or startswith(userPrincipalName,'{EscapeOData(searchTerm)}')";
            rc.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail"];
            rc.QueryParameters.Top = 20;
        }, cancellationToken);
        
        var results = new List<PrincipalInfo>();
        
        if (users?.Value is not null)
        {
            foreach (var user in users.Value)
            {
                if (user.Id is not null)
                {
                    results.Add(new PrincipalInfo(
                        Guid.Parse(user.Id),
                        user.DisplayName ?? "Unknown",
                        PrincipalType.User,
                        user.UserPrincipalName ?? user.Mail));
                }
            }
        }
        
        return results;
    }
    
    public async Task<PrincipalInfo?> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        
        var client = GetGraphClient();
        
        try
        {
            // Try by UPN first
            var user = await client.Users[email].GetAsync(rc =>
            {
                rc.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail"];
            }, cancellationToken);
            
            if (user?.Id is not null)
            {
                return new PrincipalInfo(
                    Guid.Parse(user.Id),
                    user.DisplayName ?? "Unknown",
                    PrincipalType.User,
                    user.UserPrincipalName ?? user.Mail);
            }
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // User not found by UPN, try by mail
        }
        
        // Try by mail filter
        var users = await client.Users.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = $"mail eq '{EscapeOData(email)}'";
            rc.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail"];
        }, cancellationToken);
        
        var foundUser = users?.Value?.FirstOrDefault();
        if (foundUser?.Id is not null)
        {
            return new PrincipalInfo(
                Guid.Parse(foundUser.Id),
                foundUser.DisplayName ?? "Unknown",
                PrincipalType.User,
                foundUser.UserPrincipalName ?? foundUser.Mail);
        }
        
        return null;
    }
    
    public async Task<IReadOnlyList<PrincipalInfo>> SearchServicePrincipalsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];
        
        var client = GetGraphClient();
        
        var servicePrincipals = await client.ServicePrincipals.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = $"startswith(displayName,'{EscapeOData(searchTerm)}')";
            rc.QueryParameters.Select = ["id", "displayName", "appId", "servicePrincipalType"];
            rc.QueryParameters.Top = 20;
        }, cancellationToken);
        
        var results = new List<PrincipalInfo>();
        
        if (servicePrincipals?.Value is not null)
        {
            foreach (var sp in servicePrincipals.Value)
            {
                if (sp.Id is not null)
                {
                    var principalType = sp.ServicePrincipalType?.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase) == true
                        ? PrincipalType.ManagedIdentity
                        : PrincipalType.ServicePrincipal;
                    
                    results.Add(new PrincipalInfo(
                        Guid.Parse(sp.Id),
                        sp.DisplayName ?? "Unknown",
                        principalType,
                        ApplicationId: sp.AppId));
                }
            }
        }
        
        return results;
    }
    
    public async Task<PrincipalInfo?> FindServicePrincipalAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;
        
        var client = GetGraphClient();
        
        // Try as object ID first
        if (Guid.TryParse(identifier, out var objectId))
        {
            try
            {
                var sp = await client.ServicePrincipals[identifier].GetAsync(rc =>
                {
                    rc.QueryParameters.Select = ["id", "displayName", "appId", "servicePrincipalType"];
                }, cancellationToken);
                
                if (sp?.Id is not null)
                {
                    var principalType = sp.ServicePrincipalType?.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase) == true
                        ? PrincipalType.ManagedIdentity
                        : PrincipalType.ServicePrincipal;
                    
                    return new PrincipalInfo(
                        Guid.Parse(sp.Id),
                        sp.DisplayName ?? "Unknown",
                        principalType,
                        ApplicationId: sp.AppId);
                }
            }
            catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
            {
                // Not found by object ID, try appId
            }
            
            // Try by appId
            var sps = await client.ServicePrincipals.GetAsync(rc =>
            {
                rc.QueryParameters.Filter = $"appId eq '{identifier}'";
                rc.QueryParameters.Select = ["id", "displayName", "appId", "servicePrincipalType"];
            }, cancellationToken);
            
            var foundSp = sps?.Value?.FirstOrDefault();
            if (foundSp?.Id is not null)
            {
                var principalType = foundSp.ServicePrincipalType?.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase) == true
                    ? PrincipalType.ManagedIdentity
                    : PrincipalType.ServicePrincipal;
                
                return new PrincipalInfo(
                    Guid.Parse(foundSp.Id),
                    foundSp.DisplayName ?? "Unknown",
                    principalType,
                    ApplicationId: foundSp.AppId);
            }
        }
        
        // Try by display name
        var byName = await client.ServicePrincipals.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = $"displayName eq '{EscapeOData(identifier)}'";
            rc.QueryParameters.Select = ["id", "displayName", "appId", "servicePrincipalType"];
        }, cancellationToken);
        
        var found = byName?.Value?.FirstOrDefault();
        if (found?.Id is not null)
        {
            var principalType = found.ServicePrincipalType?.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase) == true
                ? PrincipalType.ManagedIdentity
                : PrincipalType.ServicePrincipal;
            
            return new PrincipalInfo(
                Guid.Parse(found.Id),
                found.DisplayName ?? "Unknown",
                principalType,
                ApplicationId: found.AppId);
        }
        
        return null;
    }
    
    private static string EscapeOData(string value)
    {
        // Escape single quotes for OData filter
        return value.Replace("'", "''");
    }
}
