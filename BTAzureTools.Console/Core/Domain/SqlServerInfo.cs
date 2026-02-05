using Azure.Core;

namespace BTAzureTools.Core.Domain;

/// <summary>
/// Represents an Azure SQL Server.
/// </summary>
public sealed record SqlServerInfo(
    ResourceIdentifier ResourceId,
    string Name,
    string ResourceGroupName,
    string FullyQualifiedDomainName,
    Guid TenantId)
{
    public override string ToString() => $"{Name} ({FullyQualifiedDomainName})";
}
