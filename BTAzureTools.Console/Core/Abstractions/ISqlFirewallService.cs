using BTAzureTools.Core.Domain;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Service for managing Azure SQL Server firewall rules.
/// </summary>
public interface ISqlFirewallService
{
    /// <summary>
    /// Gets the current public IP address of this machine.
    /// </summary>
    Task<string> GetCurrentPublicIpAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a firewall rule exists that allows the specified IP address.
    /// </summary>
    Task<bool> IsIpAllowedAsync(
        SqlServerInfo server,
        string ipAddress,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a temporary firewall rule to allow the specified IP address.
    /// Returns the rule name for later cleanup.
    /// </summary>
    Task<string> AddTemporaryFirewallRuleAsync(
        SqlServerInfo server,
        string ipAddress,
        string ruleName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a firewall rule by name.
    /// </summary>
    Task RemoveFirewallRuleAsync(
        SqlServerInfo server,
        string ruleName,
        CancellationToken cancellationToken = default);
}
