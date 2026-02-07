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
            rc.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail", "userType"];
            rc.QueryParameters.Top = 20;
        }, cancellationToken);
        
        var results = new List<PrincipalInfo>();
        var normalizedSearchTerm = NormalizeEmail(searchTerm) ?? string.Empty;
        
        if (users?.Value is not null)
        {
            foreach (var user in users.Value
                .Where(u => u.Id is not null)
                .OrderByDescending(u => string.Equals(NormalizeEmail(u.UserPrincipalName), normalizedSearchTerm, StringComparison.Ordinal))
                .ThenByDescending(u => string.Equals(NormalizeEmail(u.Mail), normalizedSearchTerm, StringComparison.Ordinal))
                .ThenByDescending(u => string.Equals(NormalizeEmail(TryDecodeGuestUpnToExternalEmail(u.UserPrincipalName)), normalizedSearchTerm, StringComparison.Ordinal))
                .ThenBy(u => IsGuestUser(u))
                .ThenBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(ToPrincipalInfo(user));
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
        var normalizedEmail = NormalizeEmail(email) ?? string.Empty;
        
        try
        {
            // Try by UPN first
            var user = await client.Users[email].GetAsync(rc =>
            {
                rc.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail", "userType"];
            }, cancellationToken);
            
            if (user?.Id is not null && IsExactUserEmailMatch(user, normalizedEmail))
            {
                return ToPrincipalInfo(user);
            }
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // User not found by UPN, try by mail
        }
        
        // Try by exact UPN/mail filter, but avoid auto-selecting guest users for mail-only matches.
        var users = await client.Users.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = $"userPrincipalName eq '{EscapeOData(email)}' or mail eq '{EscapeOData(email)}'";
            rc.QueryParameters.Select = ["id", "displayName", "userPrincipalName", "mail", "userType"];
            rc.QueryParameters.Top = 10;
        }, cancellationToken);
        
        var candidates = users?.Value?.Where(u => u.Id is not null).ToList();
        if (candidates is null || candidates.Count == 0)
        {
            return null;
        }

        var exactUpnMatch = candidates.FirstOrDefault(u =>
            string.Equals(NormalizeEmail(u.UserPrincipalName), normalizedEmail, StringComparison.Ordinal));
        if (exactUpnMatch is not null)
        {
            return ToPrincipalInfo(exactUpnMatch);
        }

        var exactMailMemberMatch = candidates.FirstOrDefault(u =>
            string.Equals(NormalizeEmail(u.Mail), normalizedEmail, StringComparison.Ordinal) &&
            !IsGuestUser(u));
        if (exactMailMemberMatch is not null)
        {
            return ToPrincipalInfo(exactMailMemberMatch);
        }

        // If caller entered an external email in a B2B tenant, map to matching #EXT# guest UPN.
        var exactGuestAliasMatch = candidates.FirstOrDefault(u =>
            string.Equals(NormalizeEmail(TryDecodeGuestUpnToExternalEmail(u.UserPrincipalName)), normalizedEmail, StringComparison.Ordinal));
        if (exactGuestAliasMatch is not null)
        {
            return ToPrincipalInfo(exactGuestAliasMatch);
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

    private static PrincipalInfo ToPrincipalInfo(User user)
    {
        return new PrincipalInfo(
            Guid.Parse(user.Id!),
            user.DisplayName ?? "Unknown",
            PrincipalType.User,
            user.UserPrincipalName ?? user.Mail);
    }

    private static bool IsExactUserEmailMatch(User user, string normalizedEmail)
    {
        if (string.Equals(NormalizeEmail(user.UserPrincipalName), normalizedEmail, StringComparison.Ordinal))
        {
            return true;
        }

        // For member users with non-UPN email sign-in patterns, allow exact mail match.
        return !IsGuestUser(user) &&
               string.Equals(NormalizeEmail(user.Mail), normalizedEmail, StringComparison.Ordinal);
    }

    private static bool IsGuestUser(User user)
    {
        if (string.Equals(user.UserType, "Guest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return user.UserPrincipalName?.Contains("#EXT#", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static string? TryDecodeGuestUpnToExternalEmail(string? userPrincipalName)
    {
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return null;
        }

        var extIndex = userPrincipalName.IndexOf("#EXT#", StringComparison.OrdinalIgnoreCase);
        if (extIndex <= 0)
        {
            return null;
        }

        var encoded = userPrincipalName[..extIndex];
        var underscoreIndex = encoded.LastIndexOf('_');
        if (underscoreIndex <= 0 || underscoreIndex >= encoded.Length - 1)
        {
            return null;
        }

        // Guest UPN convention encodes external email as local_part + "_" + domain.
        var decoded = $"{encoded[..underscoreIndex]}@{encoded[(underscoreIndex + 1)..]}";
        return decoded;
    }
}
