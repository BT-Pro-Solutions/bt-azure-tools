namespace BTAzureTools.Core.Domain;

/// <summary>
/// Type of Entra principal.
/// </summary>
public enum PrincipalType
{
    User,
    Group,
    ServicePrincipal,
    ManagedIdentity
}

/// <summary>
/// Represents an Entra ID principal (user, group, managed identity, or service principal).
/// </summary>
public sealed record PrincipalInfo(
    Guid ObjectId,
    string DisplayName,
    PrincipalType PrincipalType,
    string? UserPrincipalName = null,
    string? ApplicationId = null)
{
    /// <summary>
    /// Gets the name to use in SQL CREATE USER statements.
    /// For users, this is the UPN. For others, it's the display name.
    /// </summary>
    public string SqlUserName => PrincipalType == PrincipalType.User && !string.IsNullOrEmpty(UserPrincipalName)
        ? UserPrincipalName
        : DisplayName;
    
    public override string ToString()
    {
        var typeLabel = PrincipalType switch
        {
            PrincipalType.User => "User",
            PrincipalType.Group => "Group",
            PrincipalType.ServicePrincipal => "App",
            PrincipalType.ManagedIdentity => "Managed Identity",
            _ => "Unknown"
        };
        
        return !string.IsNullOrEmpty(UserPrincipalName)
            ? $"{DisplayName} ({UserPrincipalName}) [{typeLabel}]"
            : $"{DisplayName} [{typeLabel}]";
    }
}
