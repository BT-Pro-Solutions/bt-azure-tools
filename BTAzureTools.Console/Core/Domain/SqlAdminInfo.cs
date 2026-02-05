namespace BTAzureTools.Core.Domain;

/// <summary>
/// Represents the current Entra admin of a SQL Server.
/// </summary>
public sealed record SqlAdminInfo(
    Guid ObjectId,
    string DisplayName,
    string? LoginName = null)
{
    public override string ToString() => !string.IsNullOrEmpty(LoginName)
        ? $"{DisplayName} ({LoginName})"
        : DisplayName;
}
