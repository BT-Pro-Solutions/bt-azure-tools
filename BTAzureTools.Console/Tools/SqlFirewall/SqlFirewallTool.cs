using BTAzureTools.Cli;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using Spectre.Console;

namespace BTAzureTools.Tools.SqlFirewall;

/// <summary>
/// Tool for adding the current IP address to Azure SQL Server firewall rules.
/// </summary>
public sealed class SqlFirewallTool : ITool
{
    private readonly AzurePrompter _prompter;
    private readonly ISqlFirewallService _firewallService;
    private readonly ICredentialProvider _credentialProvider;
    
    public ToolDescriptor Descriptor => new(
        "SQL Firewall Access",
        "sql-firewall",
        "Add your current IP to an Azure SQL Server firewall");
    
    public SqlFirewallTool(
        AzurePrompter prompter,
        ISqlFirewallService firewallService,
        ICredentialProvider credentialProvider)
    {
        _prompter = prompter;
        _firewallService = firewallService;
        _credentialProvider = credentialProvider;
    }
    
    public async Task<int> ExecuteAsync(ToolExecutionContext context)
    {
        var console = context.Console;
        var ct = context.CancellationToken;
        
        // Step 1: Select tenant
        console.MarkupLine("[bold]Step 1:[/] Select Azure Tenant");
        var tenant = await _prompter.SelectTenantAsync(console, ct);
        console.WriteLine();
        
        // Step 2: Select subscription
        console.MarkupLine("[bold]Step 2:[/] Select Subscription");
        var subscription = await _prompter.SelectSubscriptionAsync(console, tenant, ct);
        console.WriteLine();
        
        // Step 3: Select SQL server
        console.MarkupLine("[bold]Step 3:[/] Select SQL Server");
        var server = await _prompter.SelectSqlServerAsync(console, subscription, ct);
        console.WriteLine();
        
        // Step 4: Check current IP and firewall rules
        console.MarkupLine("[bold]Step 4:[/] Check Firewall Status");
        
        var currentIp = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Detecting your public IP address...", async ctx =>
                await _firewallService.GetCurrentPublicIpAsync(ct));
        
        console.MarkupLine($"Your public IP: [blue]{currentIp}[/]");
        
        var ipAllowed = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking existing firewall rules...", async ctx =>
                await _firewallService.IsIpAllowedAsync(server, currentIp, ct));
        
        if (ipAllowed)
        {
            console.MarkupLine($"[green]✓[/] Your IP address [blue]{currentIp}[/] is already allowed through the firewall.");
            console.MarkupLine("[grey]No changes needed.[/]");
            console.WriteLine();
            return 0;
        }
        
        console.MarkupLine($"[yellow]⚠[/] Your IP address [blue]{currentIp}[/] is NOT allowed through the firewall.");
        console.WriteLine();
        
        // Display confirmation summary
        DisplayConfirmationSummary(console, tenant, subscription, server, currentIp);
        
        console.WriteLine();
        console.MarkupLine("[grey]The firewall rule name clearly indicates this is for development/temporary access.[/]");
        console.WriteLine();
        
        if (!console.Confirm("[bold]Add firewall rule for your IP?[/]", true))
        {
            console.MarkupLine("[grey]Operation cancelled.[/]");
            return 0;
        }
        
        console.WriteLine();
        
        // Get current user info for rule name
        var currentUser = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Getting current user info...", async ctx =>
                await _credentialProvider.GetCurrentUserAsync(ct));
        
        var userNamePart = ExtractUserNameFromEmail(currentUser.UserPrincipalName);
        
        // Create rule name that clearly indicates it's for dev/temporary use
        var ruleName = $"DevAccess-{userNamePart}-{SanitizeMachineName(Environment.MachineName)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        
        await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Adding firewall rule for {currentIp}...", async ctx =>
            {
                await _firewallService.AddTemporaryFirewallRuleAsync(server, currentIp, ruleName, ct);
            });
        
        console.WriteLine();
        console.Write(new Rule("[green]Firewall Rule Added[/]"));
        
        var resultTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn("Details")
            .HideHeaders();
        
        resultTable.AddRow($"Rule Name: [blue]{Markup.Escape(ruleName)}[/]");
        resultTable.AddRow($"IP Address: [blue]{currentIp}[/]");
        resultTable.AddRow($"SQL Server: [blue]{Markup.Escape(server.Name)}[/]");
        
        console.Write(resultTable);
        
        console.WriteLine();
        console.MarkupLine("[grey]Note: It may take up to 5 minutes for the rule to take effect.[/]");
        console.MarkupLine($"[grey]To remove this rule later, delete [blue]{Markup.Escape(ruleName)}[/] from the SQL Server firewall settings.[/]");
        
        return 0;
    }
    
    private void DisplayConfirmationSummary(
        IAnsiConsole console,
        TenantInfo tenant,
        SubscriptionInfo subscription,
        SqlServerInfo server,
        string ipAddress)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn(new TableColumn("[bold]Setting[/]").Width(15))
            .AddColumn(new TableColumn("[bold]Value[/]"));
        
        table.AddRow("Tenant", Markup.Escape(tenant.DisplayName));
        table.AddRow("Subscription", Markup.Escape(subscription.DisplayName));
        table.AddRow("SQL Server", Markup.Escape(server.Name));
        table.AddRow("Your IP", $"[blue]{ipAddress}[/]");
        table.AddRow("Rule Name", $"[yellow]DevAccess-<user>-{SanitizeMachineName(Environment.MachineName)}-...[/]");
        
        console.Write(table);
    }
    
    /// <summary>
    /// Sanitizes the machine name to be safe for use in a firewall rule name.
    /// Azure firewall rule names can contain letters, numbers, underscores, periods, and hyphens.
    /// </summary>
    private static string SanitizeMachineName(string machineName)
    {
        var sanitized = new string(machineName
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-')
            .ToArray());
        
        // Ensure we have something, and limit length
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "Unknown";
        
        return sanitized.Length > 20 ? sanitized[..20] : sanitized;
    }
    
    /// <summary>
    /// Extracts the username part from an email address (the part before @).
    /// </summary>
    private static string ExtractUserNameFromEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return "Unknown";
        
        var atIndex = email.IndexOf('@');
        var userName = atIndex > 0 ? email[..atIndex] : email;
        
        // Sanitize for firewall rule name
        var sanitized = new string(userName
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-')
            .ToArray());
        
        if (string.IsNullOrEmpty(sanitized))
            return "Unknown";
        
        return sanitized.Length > 20 ? sanitized[..20] : sanitized;
    }
}
