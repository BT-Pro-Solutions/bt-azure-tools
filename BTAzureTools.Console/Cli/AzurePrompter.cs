using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using Spectre.Console;

namespace BTAzureTools.Cli;

/// <summary>
/// Provides reusable prompt methods for common selections using Spectre.Console.
/// </summary>
public sealed class AzurePrompter
{
    private readonly ITenantService _tenantService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ISqlServerService _sqlServerService;
    private readonly IPrincipalLookupService _principalLookupService;
    
    public AzurePrompter(
        ITenantService tenantService,
        ISubscriptionService subscriptionService,
        ISqlServerService sqlServerService,
        IPrincipalLookupService principalLookupService)
    {
        _tenantService = tenantService;
        _subscriptionService = subscriptionService;
        _sqlServerService = sqlServerService;
        _principalLookupService = principalLookupService;
    }
    
    /// <summary>
    /// Wrapper for selection prompts that includes a cancel option.
    /// </summary>
    private sealed record SelectionChoice<T>(T? Value, bool IsCancel = false)
    {
        public static SelectionChoice<T> Cancel => new(default, true);
        public static SelectionChoice<T> FromValue(T value) => new(value, false);
    }
    
    /// <summary>
    /// Prompts the user to select a tenant.
    /// </summary>
    public async Task<TenantInfo> SelectTenantAsync(
        IAnsiConsole console,
        CancellationToken cancellationToken = default)
    {
        var tenants = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading tenants...", async ctx =>
                await _tenantService.ListTenantsAsync(cancellationToken));
        
        if (tenants.Count == 0)
        {
            throw new InvalidOperationException("No tenants found. Please ensure you're logged in to Azure.");
        }
        
        if (tenants.Count == 1)
        {
            console.MarkupLine($"[grey]Using tenant:[/] [blue]{tenants[0].DisplayName}[/]");
            return tenants[0];
        }
        
        var choices = tenants.Select(SelectionChoice<TenantInfo>.FromValue).ToList();
        choices.Add(SelectionChoice<TenantInfo>.Cancel);
        
        var selected = console.Prompt(
            new SelectionPrompt<SelectionChoice<TenantInfo>>()
                .Title("[bold blue]Select a tenant:[/]")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up and down to reveal more tenants)[/]")
                .UseConverter(c => c.IsCancel ? "[grey]← Cancel[/]" : c.Value!.ToString())
                .AddChoices(choices));
        
        if (selected.IsCancel)
        {
            throw new OperationCanceledException("Tenant selection cancelled.");
        }
        
        return selected.Value!;
    }
    
    /// <summary>
    /// Prompts the user to select a subscription.
    /// </summary>
    public async Task<SubscriptionInfo> SelectSubscriptionAsync(
        IAnsiConsole console,
        TenantInfo tenant,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading subscriptions...", async ctx =>
                await _subscriptionService.ListSubscriptionsAsync(tenant.TenantId, cancellationToken));
        
        if (subscriptions.Count == 0)
        {
            throw new InvalidOperationException("No subscriptions found in this tenant.");
        }
        
        if (subscriptions.Count == 1)
        {
            console.MarkupLine($"[grey]Using subscription:[/] [blue]{subscriptions[0].DisplayName}[/]");
            return subscriptions[0];
        }
        
        var choices = subscriptions.Select(SelectionChoice<SubscriptionInfo>.FromValue).ToList();
        choices.Add(SelectionChoice<SubscriptionInfo>.Cancel);
        
        var selected = console.Prompt(
            new SelectionPrompt<SelectionChoice<SubscriptionInfo>>()
                .Title("[bold blue]Select a subscription:[/]")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up and down to reveal more subscriptions)[/]")
                .UseConverter(c => c.IsCancel ? "[grey]← Cancel[/]" : c.Value!.ToString())
                .AddChoices(choices));
        
        if (selected.IsCancel)
        {
            throw new OperationCanceledException("Subscription selection cancelled.");
        }
        
        return selected.Value!;
    }
    
    /// <summary>
    /// Prompts the user to search for and select a SQL Server.
    /// </summary>
    public async Task<SqlServerInfo> SelectSqlServerAsync(
        IAnsiConsole console,
        SubscriptionInfo subscription,
        CancellationToken cancellationToken = default)
    {
        var servers = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading SQL servers...", async ctx =>
                await _sqlServerService.ListServersAsync(subscription, cancellationToken));
        
        if (servers.Count == 0)
        {
            throw new InvalidOperationException("No SQL servers found in this subscription.");
        }
        
        // If many servers, offer search
        if (servers.Count > 10)
        {
            var searchTerm = console.Prompt(
                new TextPrompt<string>("[grey]Enter search term (or press Enter to show all):[/]")
                    .AllowEmpty());
            
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                servers = await _sqlServerService.SearchServersAsync(subscription, searchTerm, cancellationToken);
                
                if (servers.Count == 0)
                {
                    console.MarkupLine("[yellow]No servers matched your search. Showing all servers.[/]");
                    servers = await _sqlServerService.ListServersAsync(subscription, cancellationToken);
                }
            }
        }
        
        var choices = servers.Select(SelectionChoice<SqlServerInfo>.FromValue).ToList();
        choices.Add(SelectionChoice<SqlServerInfo>.Cancel);
        
        var selected = console.Prompt(
            new SelectionPrompt<SelectionChoice<SqlServerInfo>>()
                .Title("[bold blue]Select a SQL server:[/]")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up and down to reveal more servers)[/]")
                .UseConverter(c => c.IsCancel ? "[grey]← Cancel[/]" : FormatServerChoice(c.Value!))
                .AddChoices(choices));
        
        if (selected.IsCancel)
        {
            throw new OperationCanceledException("SQL server selection cancelled.");
        }
        
        return selected.Value!;
    }
    
    /// <summary>
    /// Prompts the user to select a database.
    /// </summary>
    public async Task<SqlDatabaseInfo> SelectDatabaseAsync(
        IAnsiConsole console,
        SqlServerInfo server,
        CancellationToken cancellationToken = default)
    {
        var databases = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading databases...", async ctx =>
                await _sqlServerService.ListDatabasesAsync(server, cancellationToken));
        
        if (databases.Count == 0)
        {
            throw new InvalidOperationException("No databases found on this server (excluding system databases).");
        }
        
        if (databases.Count == 1)
        {
            console.MarkupLine(
                $"[grey]Using database:[/] [blue]{Markup.Escape(databases[0].Name)}[/] [grey](RG: {Markup.Escape(databases[0].Server.ResourceGroupName)})[/]");
            return databases[0];
        }
        
        var choices = databases.Select(SelectionChoice<SqlDatabaseInfo>.FromValue).ToList();
        choices.Add(SelectionChoice<SqlDatabaseInfo>.Cancel);
        
        var selected = console.Prompt(
            new SelectionPrompt<SelectionChoice<SqlDatabaseInfo>>()
                .Title("[bold blue]Select a database:[/]")
                .PageSize(15)
                .MoreChoicesText("[grey](Move up and down to reveal more databases)[/]")
                .UseConverter(c => c.IsCancel ? "[grey]← Cancel[/]" : FormatDatabaseChoice(c.Value!))
                .AddChoices(choices));
        
        if (selected.IsCancel)
        {
            throw new OperationCanceledException("Database selection cancelled.");
        }
        
        return selected.Value!;
    }
    
    /// <summary>
    /// Prompts the user to select the principal type and then find the principal.
    /// </summary>
    public async Task<PrincipalInfo> SelectPrincipalAsync(
        IAnsiConsole console,
        CancellationToken cancellationToken = default)
    {
        var searchType = console.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold blue]What type of principal do you want to add?[/]")
                .AddChoices("User (by email)", "Managed Identity / App (by name)", "[grey]← Cancel[/]"));
        
        if (searchType.Contains("Cancel"))
        {
            throw new OperationCanceledException("Principal selection cancelled.");
        }
        
        if (searchType.StartsWith("User"))
        {
            return await SelectUserAsync(console, cancellationToken);
        }
        else
        {
            return await SelectServicePrincipalAsync(console, cancellationToken);
        }
    }
    
    private async Task<PrincipalInfo> SelectUserAsync(
        IAnsiConsole console,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var email = console.Ask<string>("[bold blue]Enter the user's email address:[/]");
            
            // Try exact match first
            var user = await console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Finding user...", async ctx =>
                    await _principalLookupService.FindUserByEmailAsync(email, cancellationToken));
            
            if (user is not null)
            {
                console.MarkupLine($"[green]Found:[/] {Markup.Escape(user.ToString())}");

                if (TryDecodeGuestUpnToExternalEmail(user.UserPrincipalName) is { } externalEmail &&
                    string.Equals(externalEmail, email.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    console.MarkupLine("[grey]Matched guest user from external email input.[/]");
                }
                
                if (console.Confirm("Use this user?", true))
                {
                    return user;
                }
            }
            else
            {
                console.MarkupLine("[grey]No exact UPN/email match found. Showing closest results (guest users may have different sign-in names).[/]");
            }
            
            // Search if not found or not confirmed
            var users = await console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Searching users...", async ctx =>
                    await _principalLookupService.SearchUsersAsync(email, cancellationToken));
            
            if (users.Count == 0)
            {
                console.MarkupLine("[yellow]No users found matching that search.[/]");
                if (!console.Confirm("Try again?", true))
                {
                    throw new OperationCanceledException("User selection cancelled.");
                }
                continue;
            }
            
            var choices = users.Select(SelectionChoice<PrincipalInfo>.FromValue).ToList();
            choices.Add(SelectionChoice<PrincipalInfo>.Cancel);
            
            var selected = console.Prompt(
                new SelectionPrompt<SelectionChoice<PrincipalInfo>>()
                    .Title("[bold blue]Select a user:[/]")
                    .PageSize(15)
                    .UseConverter(c => c.IsCancel ? "[grey]← Cancel[/]" : c.Value!.ToString())
                    .AddChoices(choices));
            
            if (selected.IsCancel)
            {
                throw new OperationCanceledException("User selection cancelled.");
            }
            
            return selected.Value!;
        }
    }
    
    private async Task<PrincipalInfo> SelectServicePrincipalAsync(
        IAnsiConsole console,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var searchTerm = console.Ask<string>("[bold blue]Enter the app/managed identity name (or object ID):[/]");
            
            // Try exact match first
            var sp = await console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Finding service principal...", async ctx =>
                    await _principalLookupService.FindServicePrincipalAsync(searchTerm, cancellationToken));
            
            if (sp is not null)
            {
                console.MarkupLine($"[green]Found:[/] {Markup.Escape(sp.ToString())}");
                
                if (console.Confirm("Use this principal?", true))
                {
                    return sp;
                }
            }
            
            // Search if not found or not confirmed
            var principals = await console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Searching service principals...", async ctx =>
                    await _principalLookupService.SearchServicePrincipalsAsync(searchTerm, cancellationToken));
            
            if (principals.Count == 0)
            {
                console.MarkupLine("[yellow]No service principals found matching that search.[/]");
                if (!console.Confirm("Try again?", true))
                {
                    throw new OperationCanceledException("Principal selection cancelled.");
                }
                continue;
            }
            
            var choices = principals.Select(SelectionChoice<PrincipalInfo>.FromValue).ToList();
            choices.Add(SelectionChoice<PrincipalInfo>.Cancel);
            
            var selected = console.Prompt(
                new SelectionPrompt<SelectionChoice<PrincipalInfo>>()
                    .Title("[bold blue]Select a service principal:[/]")
                    .PageSize(15)
                    .UseConverter(c => c.IsCancel ? "[grey]← Cancel[/]" : c.Value!.ToString())
                    .AddChoices(choices));
            
            if (selected.IsCancel)
            {
                throw new OperationCanceledException("Principal selection cancelled.");
            }
            
            return selected.Value!;
        }
    }
    
    /// <summary>
    /// Prompts the user to select a permission level.
    /// </summary>
    public PermissionLevel SelectPermissionLevel(IAnsiConsole console)
    {
        var levels = Enum.GetValues<PermissionLevel>();
        var choices = levels.Select(SelectionChoice<PermissionLevel>.FromValue).ToList();
        choices.Add(SelectionChoice<PermissionLevel>.Cancel);
        
        var selected = console.Prompt(
            new SelectionPrompt<SelectionChoice<PermissionLevel>>()
                .Title("[bold blue]Select the desired permission level:[/]")
                .UseConverter(c => c.IsCancel ? "[grey]← Cancel[/]" : c.Value.GetDescription())
                .AddChoices(choices));
        
        if (selected.IsCancel)
        {
            throw new OperationCanceledException("Permission level selection cancelled.");
        }
        
        return selected.Value;
    }

    private static string? TryDecodeGuestUpnToExternalEmail(string? userPrincipalName)
    {
        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            return null;
        }

        var extIndex = userPrincipalName.IndexOf("#EXT#", StringComparison.OrdinalIgnoreCase);
        if (extIndex <= 0)
        {
            return null;
        }

        var encoded = userPrincipalName[..extIndex];
        var underscoreIndex = encoded.LastIndexOf('_');
        if (underscoreIndex <= 0 || underscoreIndex >= encoded.Length - 1)
        {
            return null;
        }

        return $"{encoded[..underscoreIndex]}@{encoded[(underscoreIndex + 1)..]}";
    }

    private static string FormatServerChoice(SqlServerInfo server)
    {
        return $"{Markup.Escape(server.Name)} [grey](RG: {Markup.Escape(server.ResourceGroupName)} | {Markup.Escape(server.FullyQualifiedDomainName)})[/]";
    }

    private static string FormatDatabaseChoice(SqlDatabaseInfo database)
    {
        return $"{Markup.Escape(database.Name)} [grey](RG: {Markup.Escape(database.Server.ResourceGroupName)} | Server: {Markup.Escape(database.Server.Name)})[/]";
    }
}
