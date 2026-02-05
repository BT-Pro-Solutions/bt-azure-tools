using Azure.Core;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using Microsoft.Data.SqlClient;

namespace BTAzureTools.Infrastructure.Sql;

/// <summary>
/// Factory for creating authenticated SQL connections using Azure AD tokens.
/// </summary>
public sealed class AzureSqlConnectionFactory : ISqlConnectionFactory
{
    private readonly ICredentialProvider _credentialProvider;
    private const string AzureSqlScope = "https://database.windows.net/.default";
    
    public AzureSqlConnectionFactory(ICredentialProvider credentialProvider)
    {
        _credentialProvider = credentialProvider;
    }
    
    public async Task<SqlConnection> CreateConnectionAsync(
        SqlDatabaseInfo database,
        CancellationToken cancellationToken = default)
    {
        // Use tenant-scoped credential from the database's server
        var credential = _credentialProvider.GetTenantCredential(database.Server.TenantId);
        
        // Get access token for Azure SQL
        var tokenRequest = new TokenRequestContext([AzureSqlScope]);
        var token = await credential.GetTokenAsync(tokenRequest, cancellationToken);
        
        var connectionString = new SqlConnectionStringBuilder
        {
            DataSource = database.ConnectionServerName,
            InitialCatalog = database.Name,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30
        }.ConnectionString;
        
        var connection = new SqlConnection(connectionString)
        {
            AccessToken = token.Token
        };
        
        await connection.OpenAsync(cancellationToken);
        
        return connection;
    }
}
