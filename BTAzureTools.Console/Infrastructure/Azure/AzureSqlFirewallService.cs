using Azure.ResourceManager;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using System.Net;

namespace BTAzureTools.Infrastructure.AzureInfra;

/// <summary>
/// Service for managing Azure SQL Server firewall rules.
/// </summary>
public sealed class AzureSqlFirewallService : ISqlFirewallService
{
    private readonly ICredentialProvider _credentialProvider;
    private readonly HttpClient _httpClient;
    
    public AzureSqlFirewallService(ICredentialProvider credentialProvider, HttpClient httpClient)
    {
        _credentialProvider = credentialProvider;
        _httpClient = httpClient;
    }
    
    public async Task<string> GetCurrentPublicIpAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetStringAsync("https://api.ipify.org/", cancellationToken);
        var ip = response.Trim();
        
        // Validate it's a proper IP address
        if (!IPAddress.TryParse(ip, out _))
        {
            throw new InvalidOperationException($"Invalid IP address received from ipify: {ip}");
        }
        
        return ip;
    }
    
    public async Task<bool> IsIpAllowedAsync(
        SqlServerInfo server,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetTenantCredential(server.TenantId);
        var armClient = new ArmClient(credential);
        
        var sqlServerResource = armClient.GetSqlServerResource(server.ResourceId);
        var firewallRules = sqlServerResource.GetSqlFirewallRules();
        
        await foreach (var rule in firewallRules.GetAllAsync(cancellationToken: cancellationToken))
        {
            // Check if this rule covers our IP
            if (IsIpInRange(ipAddress, rule.Data.StartIPAddress, rule.Data.EndIPAddress))
            {
                return true;
            }
        }
        
        return false;
    }
    
    public async Task<string> AddTemporaryFirewallRuleAsync(
        SqlServerInfo server,
        string ipAddress,
        string ruleName,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetTenantCredential(server.TenantId);
        var armClient = new ArmClient(credential);
        
        var sqlServerResource = armClient.GetSqlServerResource(server.ResourceId);
        var firewallRules = sqlServerResource.GetSqlFirewallRules();
        
        var ruleData = new SqlFirewallRuleData
        {
            StartIPAddress = ipAddress,
            EndIPAddress = ipAddress
        };
        
        await firewallRules.CreateOrUpdateAsync(
            Azure.WaitUntil.Completed,
            ruleName,
            ruleData,
            cancellationToken);
        
        return ruleName;
    }
    
    public async Task RemoveFirewallRuleAsync(
        SqlServerInfo server,
        string ruleName,
        CancellationToken cancellationToken = default)
    {
        var credential = _credentialProvider.GetTenantCredential(server.TenantId);
        var armClient = new ArmClient(credential);
        
        var sqlServerResource = armClient.GetSqlServerResource(server.ResourceId);
        
        try
        {
            var firewallRule = await sqlServerResource.GetSqlFirewallRuleAsync(ruleName, cancellationToken);
            await firewallRule.Value.DeleteAsync(Azure.WaitUntil.Completed, cancellationToken);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Rule doesn't exist, nothing to delete
        }
    }
    
    private static bool IsIpInRange(string ipAddress, string? startIp, string? endIp)
    {
        if (string.IsNullOrEmpty(startIp) || string.IsNullOrEmpty(endIp))
            return false;
        
        if (!IPAddress.TryParse(ipAddress, out var ip))
            return false;
        
        if (!IPAddress.TryParse(startIp, out var start))
            return false;
        
        if (!IPAddress.TryParse(endIp, out var end))
            return false;
        
        var ipBytes = ip.GetAddressBytes();
        var startBytes = start.GetAddressBytes();
        var endBytes = end.GetAddressBytes();
        
        // Convert to comparable integers (for IPv4)
        var ipNum = BitConverter.ToUInt32(ipBytes.Reverse().ToArray(), 0);
        var startNum = BitConverter.ToUInt32(startBytes.Reverse().ToArray(), 0);
        var endNum = BitConverter.ToUInt32(endBytes.Reverse().ToArray(), 0);
        
        return ipNum >= startNum && ipNum <= endNum;
    }
}
