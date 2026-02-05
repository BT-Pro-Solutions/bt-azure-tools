using BTAzureTools.Core.Domain;
using Microsoft.Data.SqlClient;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Factory for creating authenticated SQL connections.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Creates an authenticated connection to the specified database.
    /// </summary>
    Task<SqlConnection> CreateConnectionAsync(
        SqlDatabaseInfo database,
        CancellationToken cancellationToken = default);
}
