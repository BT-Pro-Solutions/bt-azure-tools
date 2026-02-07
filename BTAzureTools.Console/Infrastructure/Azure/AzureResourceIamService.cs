using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.ResourceManager;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;

namespace BTAzureTools.Infrastructure.AzureInfra;

/// <summary>
/// ARM-backed implementation for resource discovery and RBAC role assignments.
/// </summary>
public sealed class AzureResourceIamService : IResourceIamService
{
    private static readonly TokenRequestContext ArmTokenRequestContext = new(["https://management.azure.com/.default"]);

    private readonly ICredentialProvider _credentialProvider;
    private readonly HttpClient _httpClient;

    public AzureResourceIamService(
        ICredentialProvider credentialProvider,
        HttpClient httpClient)
    {
        _credentialProvider = credentialProvider;
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ArmResourceInfo>> ListResourcesAsync(
        SubscriptionInfo subscription,
        SupportedResourceType resourceType,
        CancellationToken cancellationToken = default)
    {
        var filter = Uri.EscapeDataString($"resourceType eq '{resourceType.AzureResourceType}'");
        var url = $"https://management.azure.com/subscriptions/{subscription.SubscriptionId}/resources?api-version=2021-04-01&$filter={filter}";

        var resources = new List<ArmResourceInfo>();
        var values = await GetPagedValuesAsync(subscription, url, cancellationToken);

        foreach (var resource in values)
        {
            var id = TryGetString(resource, "id");
            var name = TryGetString(resource, "name");
            var type = TryGetString(resource, "type");

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            var kind = TryGetString(resource, "kind");
            if (!string.IsNullOrWhiteSpace(resourceType.KindContains) &&
                (string.IsNullOrWhiteSpace(kind) || kind.IndexOf(resourceType.KindContains, StringComparison.OrdinalIgnoreCase) < 0))
            {
                continue;
            }

            var location = TryGetString(resource, "location") ?? "unknown";
            var resourceIdentifier = new ResourceIdentifier(id);

            resources.Add(new ArmResourceInfo(
                id,
                name,
                resourceIdentifier.ResourceGroupName ?? "unknown",
                type,
                location,
                kind));
        }

        return resources
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetPrincipalRoleNamesAtScopeAsync(
        SubscriptionInfo subscription,
        string scope,
        Guid principalObjectId,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var assignmentsUrl =
            $"https://management.azure.com{normalizedScope}/providers/Microsoft.Authorization/roleAssignments?api-version=2022-04-01&$filter={Uri.EscapeDataString("atScope()")}";

        var principalId = principalObjectId.ToString();
        var roleDefinitionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var assignments = await GetPagedValuesAsync(subscription, assignmentsUrl, cancellationToken);
        foreach (var assignment in assignments)
        {
            if (!assignment.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            var assignmentPrincipalId = TryGetString(properties, "principalId");
            if (!string.Equals(assignmentPrincipalId, principalId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var roleDefinitionId = TryGetString(properties, "roleDefinitionId");
            if (!string.IsNullOrWhiteSpace(roleDefinitionId))
            {
                roleDefinitionIds.Add(roleDefinitionId);
            }
        }

        if (roleDefinitionIds.Count == 0)
        {
            return [];
        }

        var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleDefinitionId in roleDefinitionIds)
        {
            var roleName = await GetRoleNameFromDefinitionIdAsync(subscription, roleDefinitionId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(roleName))
            {
                roleNames.Add(roleName);
            }
        }

        return roleNames
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> AssignRolesAsync(
        SubscriptionInfo subscription,
        string scope,
        PrincipalInfo principal,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var requestedRoles = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedRoles.Count == 0)
        {
            return [];
        }

        var existingRoles = await GetPrincipalRoleNamesAtScopeAsync(
            subscription,
            normalizedScope,
            principal.ObjectId,
            cancellationToken);

        var existingRoleSet = new HashSet<string>(existingRoles, StringComparer.OrdinalIgnoreCase);
        var newlyAssignedRoles = new List<string>();

        foreach (var roleName in requestedRoles)
        {
            if (existingRoleSet.Contains(roleName))
            {
                continue;
            }

            var roleDefinitionId = await ResolveRoleDefinitionIdByNameAsync(subscription, roleName, cancellationToken);
            if (string.IsNullOrWhiteSpace(roleDefinitionId))
            {
                throw new InvalidOperationException(
                    $"Role definition '{roleName}' was not found in subscription '{subscription.DisplayName}'.");
            }

            var assignmentName = CreateDeterministicGuid($"{normalizedScope}|{principal.ObjectId}|{roleDefinitionId}");
            var assignmentUrl =
                $"https://management.azure.com{normalizedScope}/providers/Microsoft.Authorization/roleAssignments/{assignmentName}?api-version=2022-04-01";

            var payload = JsonSerializer.Serialize(new
            {
                properties = new
                {
                    roleDefinitionId,
                    principalId = principal.ObjectId.ToString(),
                    principalType = ToArmPrincipalType(principal.PrincipalType)
                }
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var (statusCode, responseBody) = await SendArmRequestRawAsync(
                subscription,
                HttpMethod.Put,
                assignmentUrl,
                content,
                cancellationToken);

            if (statusCode == HttpStatusCode.Conflict &&
                responseBody.Contains("RoleAssignmentExists", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if ((int)statusCode < 200 || (int)statusCode > 299)
            {
                throw BuildArmRequestException(statusCode, responseBody, HttpMethod.Put, assignmentUrl);
            }

            newlyAssignedRoles.Add(roleName);
        }

        return newlyAssignedRoles;
    }

    private async Task<IReadOnlyList<JsonElement>> GetPagedValuesAsync(
        SubscriptionInfo subscription,
        string initialUrl,
        CancellationToken cancellationToken)
    {
        var values = new List<JsonElement>();
        var nextUrl = initialUrl;

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            using var document = await SendArmRequestForJsonAsync(
                subscription,
                HttpMethod.Get,
                nextUrl,
                content: null,
                cancellationToken);

            if (document.RootElement.TryGetProperty("value", out var valueElement) &&
                valueElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in valueElement.EnumerateArray())
                {
                    values.Add(item.Clone());
                }
            }

            nextUrl = document.RootElement.TryGetProperty("nextLink", out var nextLinkElement)
                ? nextLinkElement.GetString()
                : null;
        }

        return values;
    }

    private async Task<string?> ResolveRoleDefinitionIdByNameAsync(
        SubscriptionInfo subscription,
        string roleName,
        CancellationToken cancellationToken)
    {
        var escapedRoleName = EscapeOData(roleName);
        var filter = Uri.EscapeDataString($"roleName eq '{escapedRoleName}'");
        var url =
            $"https://management.azure.com/subscriptions/{subscription.SubscriptionId}/providers/Microsoft.Authorization/roleDefinitions?api-version=2022-04-01&$filter={filter}";

        using var document = await SendArmRequestForJsonAsync(subscription, HttpMethod.Get, url, null, cancellationToken);

        if (!document.RootElement.TryGetProperty("value", out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.Array ||
            valueElement.GetArrayLength() == 0)
        {
            return null;
        }

        var first = valueElement[0];
        return TryGetString(first, "id");
    }

    private async Task<string?> GetRoleNameFromDefinitionIdAsync(
        SubscriptionInfo subscription,
        string roleDefinitionId,
        CancellationToken cancellationToken)
    {
        var url = $"https://management.azure.com{roleDefinitionId}?api-version=2022-04-01";
        using var document = await SendArmRequestForJsonAsync(subscription, HttpMethod.Get, url, null, cancellationToken);

        if (!document.RootElement.TryGetProperty("properties", out var properties))
        {
            return null;
        }

        return TryGetString(properties, "roleName");
    }

    private async Task<JsonDocument> SendArmRequestForJsonAsync(
        SubscriptionInfo subscription,
        HttpMethod method,
        string url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var (statusCode, body) = await SendArmRequestRawAsync(
            subscription,
            method,
            url,
            content,
            cancellationToken);

        if ((int)statusCode < 200 || (int)statusCode > 299)
        {
            throw BuildArmRequestException(statusCode, body, method, url);
        }

        return string.IsNullOrWhiteSpace(body)
            ? JsonDocument.Parse("{}")
            : JsonDocument.Parse(body);
    }

    private async Task<(HttpStatusCode StatusCode, string Body)> SendArmRequestRawAsync(
        SubscriptionInfo subscription,
        HttpMethod method,
        string url,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var token = await GetArmAccessTokenAsync(subscription, cancellationToken);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.StatusCode, body);
    }

    private async Task<string> GetArmAccessTokenAsync(
        SubscriptionInfo subscription,
        CancellationToken cancellationToken)
    {
        var credential = _credentialProvider.GetTenantCredential(subscription.TenantId);
        var token = await credential.GetTokenAsync(ArmTokenRequestContext, cancellationToken);
        return token.Token;
    }

    private static Exception BuildArmRequestException(
        HttpStatusCode statusCode,
        string body,
        HttpMethod method,
        string url)
    {
        var detail = ExtractArmErrorMessage(body);
        return new InvalidOperationException(
            $"ARM request failed: {(int)statusCode} {statusCode} for {method} {url}. {detail}");
    }

    private static string ExtractArmErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "No additional details were returned.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var errorElement))
            {
                return body;
            }

            var code = TryGetString(errorElement, "code");
            var message = TryGetString(errorElement, "message");

            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message))
            {
                return $"{code}: {message}";
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            return body;
        }
        catch
        {
            return body;
        }
    }

    private static string NormalizeScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Scope cannot be empty.", nameof(scope));
        }

        if (!scope.StartsWith('/'))
        {
            scope = $"/{scope}";
        }

        return scope.TrimEnd('/');
    }

    private static string EscapeOData(string value)
    {
        return value.Replace("'", "''");
    }

    private static string ToArmPrincipalType(PrincipalType principalType) => principalType switch
    {
        PrincipalType.User => "User",
        PrincipalType.Group => "Group",
        PrincipalType.ManagedIdentity => "ServicePrincipal",
        PrincipalType.ServicePrincipal => "ServicePrincipal",
        _ => "ServicePrincipal"
    };

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }
}
