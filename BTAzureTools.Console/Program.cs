using BTAzureTools.Cli;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Infrastructure.AzureInfra;
using BTAzureTools.Infrastructure.Graph;
using BTAzureTools.Infrastructure.Sql;
using BTAzureTools.Tools.SqlEntraPermissions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

// Build the service provider
var services = new ServiceCollection();

// Register credential provider (singleton - caches credentials)
services.AddSingleton<ICredentialProvider, AzureCredentialProvider>();

// Register Azure services
services.AddTransient<ITenantService, AzureTenantService>();
services.AddTransient<ISubscriptionService, AzureSubscriptionService>();
services.AddTransient<ISqlServerService, AzureSqlServerService>();
services.AddTransient<ISqlAdminService, AzureSqlAdminService>();
services.AddSingleton<HttpClient>();
services.AddTransient<ISqlFirewallService, AzureSqlFirewallService>();

// Register Graph services
services.AddTransient<IPrincipalLookupService, GraphPrincipalLookupService>();

// Register SQL services
services.AddTransient<ISqlConnectionFactory, AzureSqlConnectionFactory>();
services.AddTransient<ISqlPermissionService, SqlPermissionService>();

// Register CLI components
services.AddTransient<AzurePrompter>();
services.AddSingleton<IToolRegistry, ToolRegistry>();
services.AddTransient<ToolMenu>();

// Register tools
services.AddTransient<SqlEntraPermissionsTool>();

var serviceProvider = services.BuildServiceProvider();

// Register tools with the registry
var toolRegistry = serviceProvider.GetRequiredService<IToolRegistry>();
toolRegistry.Register<SqlEntraPermissionsTool>();

// Run the tool menu
var console = AnsiConsole.Console;
var menu = serviceProvider.GetRequiredService<ToolMenu>();

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var exitCode = await menu.RunAsync(console, cts.Token);
    return exitCode;
}
catch (OperationCanceledException)
{
    console.MarkupLine("[yellow]Operation cancelled.[/]");
    return 1;
}
catch (Exception ex)
{
    console.WriteException(ex, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
    return 1;
}