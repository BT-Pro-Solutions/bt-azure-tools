namespace BTAzureTools.Core.Domain;

/// <summary>
/// Database permission levels that can be assigned to a user.
/// </summary>
public enum PermissionLevel
{
    /// <summary>
    /// Full database owner - all permissions including schema changes.
    /// Roles: db_owner
    /// </summary>
    FullAdmin,
    
    /// <summary>
    /// Full application-level access - read, write, execute stored procedures.
    /// Roles: db_datareader, db_datawriter, plus EXECUTE permission
    /// </summary>
    FullAppLevel,
    
    /// <summary>
    /// Restricted application access - read and write data only.
    /// Roles: db_datareader, db_datawriter
    /// </summary>
    RestrictedAppLevel,
    
    /// <summary>
    /// Read-only access to data.
    /// Roles: db_datareader
    /// </summary>
    ReadOnly,
    
    /// <summary>
    /// Remove all access (drop user).
    /// </summary>
    None
}

public static class PermissionLevelExtensions
{
    public static string GetDescription(this PermissionLevel level) => level switch
    {
        PermissionLevel.FullAdmin => "Full Admin (db_owner - all permissions)",
        PermissionLevel.FullAppLevel => "Full App-Level (read/write/execute)",
        PermissionLevel.RestrictedAppLevel => "Restricted App-Level (read/write only)",
        PermissionLevel.ReadOnly => "Read-Only (db_datareader)",
        PermissionLevel.None => "None (remove user)",
        _ => level.ToString()
    };
}
