using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for managing Azure SQL Server Entra ID administrators.
/// </summary>
public interface ISqlAdminService
{
    /// <summary>
    /// Gets the current Entra admin for the SQL Server.
    /// </summary>
    Task<SqlAdminInfo?> GetCurrentAdminAsync(
        SqlServerInfo server,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets the Entra admin for the SQL Server.
    /// </summary>
    Task SetAdminAsync(
        SqlServerInfo server,
        PrincipalInfo principal,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Temporarily elevates the current user to SQL admin, returning a scope that restores the original admin when disposed.
    /// </summary>
    Task<IAdminElevationScope> ElevateCurrentUserAsync(
        SqlServerInfo server,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an elevated admin session that can be restored.
/// </summary>
public interface IAdminElevationScope : IAsyncDisposable
{
    /// <summary>
    /// The original admin before elevation.
    /// </summary>
    SqlAdminInfo? OriginalAdmin { get; }
    
    /// <summary>
    /// Suppress the automatic restoration of the original admin on dispose.
    /// </summary>
    void SuppressRestore();
}
