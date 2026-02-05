using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using Microsoft.Data.SqlClient;

namespace BTAzureTools.Infrastructure.Sql;

/// <summary>
/// Service for managing SQL database user permissions.
/// </summary>
public sealed class SqlPermissionService : ISqlPermissionService
{
    private readonly ISqlConnectionFactory _connectionFactory;
    
    // Database roles for each permission level
    private static readonly Dictionary<PermissionLevel, string[]> RoleMemberships = new()
    {
        [PermissionLevel.FullAdmin] = ["db_owner"],
        [PermissionLevel.FullAppLevel] = ["db_datareader", "db_datawriter"],
        [PermissionLevel.RestrictedAppLevel] = ["db_datareader", "db_datawriter"],
        [PermissionLevel.ReadOnly] = ["db_datareader"],
        [PermissionLevel.None] = []
    };
    
    // All roles we manage
    private static readonly string[] AllManagedRoles = ["db_owner", "db_datareader", "db_datawriter"];
    
    public SqlPermissionService(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    
    public async Task<bool> UserExistsAsync(
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(database, cancellationToken);
        
        var sql = @"
            SELECT COUNT(*) 
            FROM sys.database_principals 
            WHERE name = @UserName AND type_desc IN ('EXTERNAL_USER', 'EXTERNAL_GROUP')";
        
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserName", principal.SqlUserName);
        
        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }
    
    public async Task<PermissionLevel?> GetUserPermissionLevelAsync(
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        CancellationToken cancellationToken = default)
    {
        if (!await UserExistsAsync(database, principal, cancellationToken))
            return null;
        
        await using var connection = await _connectionFactory.CreateConnectionAsync(database, cancellationToken);
        
        // Get user's role memberships
        var sql = @"
            SELECT r.name AS RoleName
            FROM sys.database_role_members rm
            INNER JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
            INNER JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
            WHERE u.name = @UserName";
        
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserName", principal.SqlUserName);
        
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(reader.GetString(0));
        }
        
        // Determine permission level from roles
        if (roles.Contains("db_owner"))
            return PermissionLevel.FullAdmin;
        
        if (roles.Contains("db_datareader") && roles.Contains("db_datawriter"))
            return PermissionLevel.FullAppLevel; // Could be RestrictedAppLevel too, but we can't distinguish
        
        if (roles.Contains("db_datareader"))
            return PermissionLevel.ReadOnly;
        
        return null; // User exists but has no standard roles
    }
    
    public async Task ApplyPermissionsAsync(
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        PermissionLevel level,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(database, cancellationToken);
        
        var userExists = await UserExistsInternalAsync(connection, principal.SqlUserName, cancellationToken);
        
        if (level == PermissionLevel.None)
        {
            // Remove user if they exist
            if (userExists)
            {
                await DropUserAsync(connection, principal.SqlUserName, cancellationToken);
            }
            return;
        }
        
        // Create user if they don't exist
        if (!userExists)
        {
            await CreateUserAsync(connection, principal, cancellationToken);
        }
        
        // Get desired roles for this level
        var desiredRoles = RoleMemberships[level];
        
        // Remove user from all managed roles first
        foreach (var role in AllManagedRoles)
        {
            if (await IsInRoleAsync(connection, principal.SqlUserName, role, cancellationToken))
            {
                await RemoveFromRoleAsync(connection, principal.SqlUserName, role, cancellationToken);
            }
        }
        
        // Add user to desired roles
        foreach (var role in desiredRoles)
        {
            await AddToRoleAsync(connection, principal.SqlUserName, role, cancellationToken);
        }
        
        // For FullAppLevel, also grant EXECUTE permission on schema
        if (level == PermissionLevel.FullAppLevel)
        {
            await GrantExecuteAsync(connection, principal.SqlUserName, cancellationToken);
        }
    }
    
    private static async Task<bool> UserExistsInternalAsync(
        SqlConnection connection,
        string userName,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT COUNT(*) 
            FROM sys.database_principals 
            WHERE name = @UserName AND type_desc IN ('EXTERNAL_USER', 'EXTERNAL_GROUP')";
        
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserName", userName);
        
        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }
    
    private static async Task CreateUserAsync(
        SqlConnection connection,
        PrincipalInfo principal,
        CancellationToken cancellationToken)
    {
        // CREATE USER [name] FROM EXTERNAL PROVIDER
        // We need to use dynamic SQL for the username since it can't be parameterized
        var sql = $"CREATE USER [{EscapeSqlIdentifier(principal.SqlUserName)}] FROM EXTERNAL PROVIDER";
        
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    private static async Task DropUserAsync(
        SqlConnection connection,
        string userName,
        CancellationToken cancellationToken)
    {
        var sql = $"DROP USER [{EscapeSqlIdentifier(userName)}]";
        
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    private static async Task<bool> IsInRoleAsync(
        SqlConnection connection,
        string userName,
        string roleName,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT COUNT(*)
            FROM sys.database_role_members rm
            INNER JOIN sys.database_principals u ON rm.member_principal_id = u.principal_id
            INNER JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
            WHERE u.name = @UserName AND r.name = @RoleName";
        
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@UserName", userName);
        command.Parameters.AddWithValue("@RoleName", roleName);
        
        var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        return count > 0;
    }
    
    private static async Task AddToRoleAsync(
        SqlConnection connection,
        string userName,
        string roleName,
        CancellationToken cancellationToken)
    {
        var sql = $"ALTER ROLE [{EscapeSqlIdentifier(roleName)}] ADD MEMBER [{EscapeSqlIdentifier(userName)}]";
        
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    private static async Task RemoveFromRoleAsync(
        SqlConnection connection,
        string userName,
        string roleName,
        CancellationToken cancellationToken)
    {
        var sql = $"ALTER ROLE [{EscapeSqlIdentifier(roleName)}] DROP MEMBER [{EscapeSqlIdentifier(userName)}]";
        
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    private static async Task GrantExecuteAsync(
        SqlConnection connection,
        string userName,
        CancellationToken cancellationToken)
    {
        // Grant execute on all schemas
        var sql = $"GRANT EXECUTE TO [{EscapeSqlIdentifier(userName)}]";
        
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    
    private static string EscapeSqlIdentifier(string identifier)
    {
        // Escape ] characters by doubling them
        return identifier.Replace("]", "]]");
    }
    
    public async Task<bool> CanCurrentUserManageUsersAsync(
        SqlDatabaseInfo database,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(database, cancellationToken);
            
            // Check if current user is db_owner or has ALTER ANY USER permission
            var sql = @"
                SELECT CASE 
                    WHEN IS_ROLEMEMBER('db_owner') = 1 THEN 1
                    WHEN HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER ANY USER') = 1 THEN 1
                    ELSE 0
                END";
            
            await using var command = new SqlCommand(sql, connection);
            var result = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            return result == 1;
        }
        catch
        {
            // Connection failed or permission check failed - user can't manage users
            return false;
        }
    }
}
