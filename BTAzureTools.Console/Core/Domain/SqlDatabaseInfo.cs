using Azure.Core;

namespace BTAzureTools.Core.Domain;

/// <summary>
/// Represents an Azure SQL Database.
/// </summary>
public sealed record SqlDatabaseInfo(
    ResourceIdentifier ResourceId,
    string Name,
    SqlServerInfo Server)
{
    public override string ToString() => $"{Name} (on {Server.Name})";
    
    public string ConnectionServerName => Server.FullyQualifiedDomainName;
}
