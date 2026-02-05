using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for applying database permissions to users.
/// </summary>
public interface ISqlPermissionService
{
    /// <summary>
    /// Checks if a user exists in the database.
    /// </summary>
    Task<bool> UserExistsAsync(
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current permission level for a user.
    /// </summary>
    Task<PermissionLevel?> GetUserPermissionLevelAsync(
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Applies the specified permission level to a user.
    /// Creates the user if they don't exist, or modifies their permissions if they do.
    /// If level is None, removes the user.
    /// </summary>
    Task ApplyPermissionsAsync(
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        PermissionLevel level,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if the current user can manage database users (has db_owner or ALTER ANY USER).
    /// Returns false if the user cannot connect or lacks permissions.
    /// </summary>
    Task<bool> CanCurrentUserManageUsersAsync(
        SqlDatabaseInfo database,
        CancellationToken cancellationToken = default);
}
