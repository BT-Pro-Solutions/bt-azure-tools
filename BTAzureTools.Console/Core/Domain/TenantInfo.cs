namespace BTAzureTools.Core.Domain;

/// <summary>
/// Represents an Azure AD/Entra tenant.
/// </summary>
public sealed record TenantInfo(
    Guid TenantId,
    string DisplayName,
    string? DefaultDomain = null)
{
    public override string ToString() => string.IsNullOrEmpty(DefaultDomain) 
        ? $"{DisplayName} ({TenantId})" 
        : $"{DisplayName} ({DefaultDomain})";
}
