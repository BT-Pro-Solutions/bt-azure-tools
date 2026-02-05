using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for managing Azure SQL Server resources.
/// </summary>
public interface ISqlServerService
{
    /// <summary>
    /// Lists all SQL Servers in the subscription.
    /// </summary>
    Task<IReadOnlyList<SqlServerInfo>> ListServersAsync(
        SubscriptionInfo subscription,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for SQL Servers by name.
    /// </summary>
    Task<IReadOnlyList<SqlServerInfo>> SearchServersAsync(
        SubscriptionInfo subscription,
        string searchTerm,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists all databases on a SQL Server.
    /// </summary>
    Task<IReadOnlyList<SqlDatabaseInfo>> ListDatabasesAsync(
        SqlServerInfo server,
        CancellationToken cancellationToken = default);
}
