using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;

namespace BTAzureTools.Infrastructure.AzureInfra;

/// <summary>
/// Service for managing Azure SQL Server Entra ID administrators.
/// </summary>
public sealed class AzureSqlAdminService : ISqlAdminService
{
    private readonly ICredentialProvider _credentialProvider;
    
    public AzureSqlAdminService(ICredentialProvider credentialProvider)
    {
        _credentialProvider = credentialProvider;
    }
    
    public async Task<SqlAdminInfo?> GetCurrentAdminAsync(
        SqlServerInfo server,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetTenantCredential(server.TenantId);
        var armClient = new ArmClient(credential);
        
        var sqlServerResource = armClient.GetSqlServerResource(server.ResourceId);
        var admins = sqlServerResource.GetSqlServerAzureADAdministrators();
        
        try
        {
            // There can only be one Entra admin, and it's always named "ActiveDirectory"
            var admin = await admins.GetAsync("ActiveDirectory", cancellationToken);
            
            if (admin?.Value?.Data is not null && admin.Value.Data.Sid.HasValue)
            {
                return new SqlAdminInfo(
                    admin.Value.Data.Sid.Value,
                    admin.Value.Data.Login ?? "Unknown",
                    admin.Value.Data.Login);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // No admin configured
        }
        
        return null;
    }
    
    public async Task SetAdminAsync(
        SqlServerInfo server,
        PrincipalInfo principal,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetTenantCredential(server.TenantId);
        var armClient = new ArmClient(credential);
        
        var sqlServerResource = armClient.GetSqlServerResource(server.ResourceId);
        var admins = sqlServerResource.GetSqlServerAzureADAdministrators();
        
        var adminData = new SqlServerAzureADAdministratorData
        {
            AdministratorType = SqlAdministratorType.ActiveDirectory,
            Login = principal.SqlUserName,
            Sid = principal.ObjectId,
            TenantId = null // Will be inferred
        };
        
        await admins.CreateOrUpdateAsync(
            WaitUntil.Completed,
            "ActiveDirectory",
            adminData,
            cancellationToken);
    }
    
    public async Task<IAdminElevationScope> ElevateCurrentUserAsync(
        SqlServerInfo server,
        CancellationToken cancellationToken = default)
    {
        // Get current admin
        var originalAdmin = await GetCurrentAdminAsync(server, cancellationToken);
        
        // Get current user info
        var (objectId, displayName, upn) = await _credentialProvider.GetCurrentUserAsync(cancellationToken);
        
        var currentUserPrincipal = new PrincipalInfo(
            objectId,
            displayName,
            PrincipalType.User,
            upn);
        
        // Set current user as admin
        await SetAdminAsync(server, currentUserPrincipal, cancellationToken);
        
        return new AdminElevationScope(this, server, originalAdmin);
    }
    
    private sealed class AdminElevationScope : IAdminElevationScope
    {
        private readonly AzureSqlAdminService _service;
        private readonly SqlServerInfo _server;
        private bool _suppressRestore;
        private bool _disposed;
        
        public SqlAdminInfo? OriginalAdmin { get; }
        
        public AdminElevationScope(
            AzureSqlAdminService service,
            SqlServerInfo server,
            SqlAdminInfo? originalAdmin)
        {
            _service = service;
            _server = server;
            OriginalAdmin = originalAdmin;
        }
        
        public void SuppressRestore()
        {
            _suppressRestore = true;
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            
            _disposed = true;
            
            if (_suppressRestore || OriginalAdmin is null)
                return;
            
            try
            {
                // Restore original admin
                var originalPrincipal = new PrincipalInfo(
                    OriginalAdmin.ObjectId,
                    OriginalAdmin.DisplayName,
                    PrincipalType.User, // We don't know the original type, assume user
                    OriginalAdmin.LoginName);
                
                await _service.SetAdminAsync(_server, originalPrincipal, CancellationToken.None);
            }
            catch
            {
                // Log but don't throw - best effort restoration
            }
        }
    }
}
