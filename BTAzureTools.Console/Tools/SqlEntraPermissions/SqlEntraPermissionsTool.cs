using BTAzureTools.Cli;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using Spectre.Console;

namespace BTAzureTools.Tools.SqlEntraPermissions;

/// <summary>
/// Tool for managing Entra ID user permissions on Azure SQL databases.
/// </summary>
public sealed class SqlEntraPermissionsTool : ITool
{
    private readonly AzurePrompter _prompter;
    private readonly ISqlAdminService _sqlAdminService;
    private readonly ISqlPermissionService _permissionService;
    private readonly ISqlFirewallService _firewallService;
    
    public ToolDescriptor Descriptor => new(
        "SQL Entra Permissions",
        "sql-entra-perms",
        "Add/modify Entra ID user access to Azure SQL databases");
    
    public SqlEntraPermissionsTool(
        AzurePrompter prompter,
        ISqlAdminService sqlAdminService,
        ISqlPermissionService permissionService,
        ISqlFirewallService firewallService)
    {
        _prompter = prompter;
        _sqlAdminService = sqlAdminService;
        _permissionService = permissionService;
        _firewallService = firewallService;
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
        
        // Step 3.5: Check firewall rules
        string? temporaryFirewallRule = null;
        var currentIp = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Detecting your public IP address...", async ctx =>
                await _firewallService.GetCurrentPublicIpAsync(ct));
        
        var ipAllowed = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking firewall rules...", async ctx =>
                await _firewallService.IsIpAllowedAsync(server, currentIp, ct));
        
        if (!ipAllowed)
        {
            console.MarkupLine($"[yellow]⚠[/] Your IP address [blue]{currentIp}[/] is not allowed through the SQL Server firewall.");
            
            if (console.Confirm("Add a temporary firewall rule for your IP?", true))
            {
                var ruleName = $"BTAzureTools-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                
                await console.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync($"Adding firewall rule for {currentIp}...", async ctx =>
                    {
                        temporaryFirewallRule = await _firewallService.AddTemporaryFirewallRuleAsync(
                            server, currentIp, ruleName, ct);
                    });
                
                console.MarkupLine($"[green]✓[/] Firewall rule [blue]{temporaryFirewallRule}[/] added");
                console.MarkupLine("[grey]  Note: It may take up to 5 minutes for the rule to take effect.[/]");
                console.WriteLine();
            }
            else
            {
                console.MarkupLine("[yellow]Continuing without firewall rule. Database connection may fail.[/]");
                console.WriteLine();
            }
        }
        else
        {
            console.MarkupLine($"[green]✓[/] Your IP [blue]{currentIp}[/] is allowed through the firewall");
            console.WriteLine();
        }
        
        // Step 4: Select database
        console.MarkupLine("[bold]Step 4:[/] Select Database");
        var database = await _prompter.SelectDatabaseAsync(console, server, ct);
        console.WriteLine();
        
        // Check if current user can already manage users in this database
        var canManageUsers = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking if you have database admin permissions...", async ctx =>
                await _permissionService.CanCurrentUserManageUsersAsync(database, ct));
        
        if (canManageUsers)
        {
            console.MarkupLine("[green]✓[/] You already have permissions to manage users in this database");
        }
        else
        {
            console.MarkupLine("[grey]You don't currently have user management permissions - will need admin elevation[/]");
        }
        console.WriteLine();
        
        // Step 5: Select principal (user or managed identity)
        console.MarkupLine("[bold]Step 5:[/] Select User/Principal");
        var principal = await _prompter.SelectPrincipalAsync(console, ct);
        console.WriteLine();
        
        // Step 6: Select permission level
        console.MarkupLine("[bold]Step 6:[/] Select Permission Level");
        var permissionLevel = _prompter.SelectPermissionLevel(console);
        console.WriteLine();
        
        // Check current state
        var currentPermission = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking current permissions...", async ctx =>
            {
                try
                {
                    return await _permissionService.GetUserPermissionLevelAsync(database, principal, ct);
                }
                catch
                {
                    // May fail if we don't have access yet
                    return null;
                }
            });
        
        // Get current admin (only needed if we'll need elevation)
        SqlAdminInfo? currentAdmin = null;
        if (!canManageUsers)
        {
            currentAdmin = await console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Checking current SQL admin...", async ctx =>
                    await _sqlAdminService.GetCurrentAdminAsync(server, ct));
        }
        
        // Display confirmation summary
        console.WriteLine();
        DisplayConfirmationSummary(console, tenant, subscription, server, database, principal, permissionLevel, currentPermission, currentAdmin);
        
        // Warning about admin change (only if we need to elevate)
        console.WriteLine();
        if (canManageUsers)
        {
            console.MarkupLine("[green]✓[/] You have database permissions to manage users. No server admin change needed.");
        }
        else
        {
            console.MarkupLine("[yellow]⚠ WARNING:[/] This operation will temporarily change the SQL Server Entra admin to your logged-in user.");
            console.MarkupLine("[yellow]  If someone is currently using the admin account for database access, they may be briefly disrupted.[/]");
            
            if (currentAdmin is not null)
            {
                console.MarkupLine($"[yellow]  Current admin: {Markup.Escape(currentAdmin.ToString())}[/]");
            }
            else
            {
                console.MarkupLine("[yellow]  No current Entra admin is set. One will be configured.[/]");
            }
        }
        
        console.WriteLine();
        
        if (!console.Confirm("[bold]Proceed with these changes?[/]", false))
        {
            console.MarkupLine("[grey]Operation cancelled.[/]");
            return 0;
        }
        
        console.WriteLine();
        
        // Execute the operation
        await ExecutePermissionChangeAsync(console, server, database, principal, permissionLevel, currentAdmin, canManageUsers, temporaryFirewallRule, ct);
        
        return 0;
    }
    
    private void DisplayConfirmationSummary(
        IAnsiConsole console,
        TenantInfo tenant,
        SubscriptionInfo subscription,
        SqlServerInfo server,
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        PermissionLevel permissionLevel,
        PermissionLevel? currentPermission,
        SqlAdminInfo? currentAdmin)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn(new TableColumn("[bold]Setting[/]").Width(20))
            .AddColumn(new TableColumn("[bold]Value[/]"));
        
        table.AddRow("Tenant", Markup.Escape(tenant.DisplayName));
        table.AddRow("Subscription", Markup.Escape(subscription.DisplayName));
        table.AddRow("SQL Server", Markup.Escape(server.Name));
        table.AddRow("Database", Markup.Escape(database.Name));
        table.AddRow("Principal", Markup.Escape(principal.ToString()));
        table.AddRow("Principal Type", principal.PrincipalType.ToString());
        
        if (currentPermission.HasValue)
        {
            table.AddRow("Current Permission", $"[yellow]{currentPermission.Value.GetDescription()}[/]");
        }
        else
        {
            table.AddRow("Current Permission", "[grey]None (user does not exist)[/]");
        }
        
        var newPermColor = permissionLevel == PermissionLevel.None ? "red" : "green";
        table.AddRow("New Permission", $"[{newPermColor}]{permissionLevel.GetDescription()}[/]");
        
        console.Write(table);
    }
    
    private async Task ExecutePermissionChangeAsync(
        IAnsiConsole console,
        SqlServerInfo server,
        SqlDatabaseInfo database,
        PrincipalInfo principal,
        PermissionLevel permissionLevel,
        SqlAdminInfo? originalAdmin,
        bool canAlreadyManageUsers,
        string? temporaryFirewallRule,
        CancellationToken ct)
    {
        IAdminElevationScope? adminScope = null;
        
        try
        {
            // Step 1: Elevate current user to admin (only if they can't already manage users)
            if (!canAlreadyManageUsers)
            {
                await console.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Elevating your user to SQL admin...", async ctx =>
                    {
                        adminScope = await _sqlAdminService.ElevateCurrentUserAsync(server, ct);
                    });
                
                console.MarkupLine("[green]✓[/] Elevated to SQL admin");
                
                // Give Azure a moment to propagate the change
                await console.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Waiting for admin change to propagate...", async ctx =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    });
            }
            else
            {
                console.MarkupLine("[green]✓[/] Using existing database permissions");
            }
            
            // Step 2: Apply permissions
            await console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Applying {permissionLevel.GetDescription()} permissions...", async ctx =>
                {
                    await _permissionService.ApplyPermissionsAsync(database, principal, permissionLevel, ct);
                });
            
            if (permissionLevel == PermissionLevel.None)
            {
                console.MarkupLine($"[green]✓[/] User [blue]{Markup.Escape(principal.SqlUserName)}[/] removed from database");
            }
            else
            {
                console.MarkupLine($"[green]✓[/] Permissions applied: [blue]{permissionLevel.GetDescription()}[/]");
            }
            
            // Step 3: Ask about restoring original admin (only if we elevated)
            console.WriteLine();
            
            if (!canAlreadyManageUsers)
            {
                if (originalAdmin is not null)
                {
                    var restoreAdmin = console.Confirm(
                        $"Restore original admin ([blue]{Markup.Escape(originalAdmin.DisplayName)}[/])?",
                        true);
                    
                    if (!restoreAdmin)
                    {
                        adminScope?.SuppressRestore();
                        console.MarkupLine("[yellow]⚠[/] Original admin will NOT be restored. Your user remains as admin.");
                    }
                }
                else
                {
                    var keepAsAdmin = console.Confirm("No original admin was set. Keep yourself as admin?", true);
                    
                    if (keepAsAdmin)
                    {
                        adminScope?.SuppressRestore();
                    }
                }
            }
        }
        finally
        {
            // Dispose will restore original admin if not suppressed
            if (adminScope is not null)
            {
                await console.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Finalizing admin settings...", async ctx =>
                    {
                        await adminScope.DisposeAsync();
                    });
                
                if (originalAdmin is not null)
                {
                    console.MarkupLine($"[green]✓[/] Restored original admin: [blue]{Markup.Escape(originalAdmin.DisplayName)}[/]");
                }
            }
            
            // Clean up temporary firewall rule if one was created
            if (temporaryFirewallRule is not null)
            {
                var removeRule = console.Confirm(
                    $"Remove temporary firewall rule ([blue]{temporaryFirewallRule}[/])?",
                    true);
                
                if (removeRule)
                {
                    await console.Status()
                        .Spinner(Spinner.Known.Dots)
                        .StartAsync("Removing firewall rule...", async ctx =>
                        {
                            await _firewallService.RemoveFirewallRuleAsync(server, temporaryFirewallRule, ct);
                        });
                    
                    console.MarkupLine($"[green]✓[/] Firewall rule removed");
                }
                else
                {
                    console.MarkupLine($"[yellow]⚠[/] Firewall rule [blue]{temporaryFirewallRule}[/] left in place");
                }
            }
        }
        
        console.WriteLine();
        console.Write(new Rule("[green]Operation Complete[/]"));
        
        // Show final summary
        var finalTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn("Summary")
            .HideHeaders();
        
        finalTable.AddRow($"Database: [blue]{Markup.Escape(database.Name)}[/]");
        finalTable.AddRow($"User: [blue]{Markup.Escape(principal.SqlUserName)}[/]");
        
        if (permissionLevel == PermissionLevel.None)
        {
            finalTable.AddRow("[yellow]User has been removed from the database[/]");
        }
        else
        {
            finalTable.AddRow($"Permission Level: [green]{permissionLevel.GetDescription()}[/]");
        }
        
        console.Write(finalTable);
    }
}
