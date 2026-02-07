using BTAzureTools.Cli;
using BTAzureTools.Core.Abstractions;
using BTAzureTools.Core.Domain;
using Spectre.Console;

namespace BTAzureTools.Tools.ResourceIam;

/// <summary>
/// Tool for assigning Entra principals to Azure resources using RBAC roles.
/// </summary>
public sealed class ResourceIamTool : ITool
{
    private readonly AzurePrompter _prompter;
    private readonly IResourceIamService _resourceIamService;

    public ToolDescriptor Descriptor => new(
        "Resource IAM Assignment",
        "resource-iam",
        "Search resources and assign Entra RBAC roles by access level");

    public ResourceIamTool(
        AzurePrompter prompter,
        IResourceIamService resourceIamService)
    {
        _prompter = prompter;
        _resourceIamService = resourceIamService;
    }

    public async Task<int> ExecuteAsync(ToolExecutionContext context)
    {
        var console = context.Console;
        var ct = context.CancellationToken;

        console.MarkupLine("[bold]Step 1:[/] Select Azure Tenant");
        var tenant = await _prompter.SelectTenantAsync(console, ct);
        console.WriteLine();

        console.MarkupLine("[bold]Step 2:[/] Select Subscription");
        var subscription = await _prompter.SelectSubscriptionAsync(console, tenant, ct);
        console.WriteLine();

        while (true)
        {
            console.MarkupLine("[bold]Step 3:[/] Select Resource Type");
            var resourceType = SelectResourceType(console);
            console.WriteLine();

            console.MarkupLine("[bold]Step 4:[/] Search and Select Resource");
            var resource = await SelectResourceAsync(console, subscription, resourceType, ct);
            console.WriteLine();

            if (resource is null)
            {
                if (console.Confirm("Try a different resource type?", true))
                {
                    console.WriteLine();
                    continue;
                }

                console.MarkupLine("[grey]Operation cancelled.[/]");
                return 0;
            }

            console.MarkupLine("[bold]Step 5:[/] Select Access Level");
            var accessLevel = SelectAccessLevel(console, resourceType);
            console.WriteLine();

            console.MarkupLine("[bold]Step 6:[/] Select User/Identity");
            var principal = await _prompter.SelectPrincipalAsync(console, ct);
            console.WriteLine();

            var currentRoles = await console.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Checking existing role assignments...", async _ =>
                    await _resourceIamService.GetPrincipalRoleNamesAtScopeAsync(
                        subscription,
                        resource.ResourceId,
                        principal.ObjectId,
                        ct));

            var currentRoleSet = new HashSet<string>(currentRoles, StringComparer.OrdinalIgnoreCase);

            var missingRoles = accessLevel.RoleNames
                .Where(role => !currentRoleSet.Contains(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            DisplayConfirmationSummary(
                console,
                tenant,
                subscription,
                resourceType,
                resource,
                accessLevel,
                principal,
                currentRoles,
                missingRoles);

            if (missingRoles.Count == 0)
            {
                console.WriteLine();
                console.MarkupLine("[green]✓[/] No changes required. Principal already has all roles for this access level.");
            }
            else
            {
                console.WriteLine();
                if (!console.Confirm("[bold]Proceed with role assignment?[/]", false))
                {
                    console.MarkupLine("[grey]Assignment skipped.[/]");
                }
                else
                {
                    console.WriteLine();
                    var newlyAssignedRoles = await console.Status()
                        .Spinner(Spinner.Known.Dots)
                        .StartAsync("Applying RBAC role assignments...", async _ =>
                            await _resourceIamService.AssignRolesAsync(
                                subscription,
                                resource.ResourceId,
                                principal,
                                accessLevel.RoleNames,
                                ct));

                    console.MarkupLine("[green]✓[/] Role assignment complete.");
                    foreach (var role in newlyAssignedRoles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                    {
                        console.MarkupLine($"[green]  +[/] {Markup.Escape(role)}");
                    }
                }
            }

            console.WriteLine();
            if (!console.Confirm("Assign permissions for another resource?", true))
            {
                console.Write(new Rule("[green]Operation Complete[/]"));
                return 0;
            }

            console.WriteLine();
        }
    }

    private static SupportedResourceType SelectResourceType(IAnsiConsole console)
    {
        var choices = ResourceIamCatalog.SupportedTypes
            .Select(SelectionChoice<SupportedResourceType>.FromValue)
            .ToList();

        choices.Add(SelectionChoice<SupportedResourceType>.Cancel);

        var selected = console.Prompt(
            new SelectionPrompt<SelectionChoice<SupportedResourceType>>()
                .Title("[bold blue]Select a supported resource type:[/]")
                .PageSize(12)
                .UseConverter(c =>
                    c.IsCancel
                        ? "[grey]← Cancel[/]"
                        : $"{Markup.Escape(c.Value!.DisplayName)} [grey]- {Markup.Escape(c.Value.Description)}[/]")
                .AddChoices(choices));

        if (selected.IsCancel)
        {
            throw new OperationCanceledException("Resource type selection cancelled.");
        }

        return selected.Value!;
    }

    private async Task<ArmResourceInfo?> SelectResourceAsync(
        IAnsiConsole console,
        SubscriptionInfo subscription,
        SupportedResourceType resourceType,
        CancellationToken cancellationToken)
    {
        var resources = await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Loading resources...", async _ =>
                await _resourceIamService.ListResourcesAsync(subscription, resourceType, cancellationToken));

        if (resources.Count == 0)
        {
            console.MarkupLine(
                $"[yellow]No {Markup.Escape(resourceType.DisplayName)} resources were found in subscription '{Markup.Escape(subscription.DisplayName)}'.[/]");
            return null;
        }

        while (true)
        {
            var searchTerm = console.Prompt(
                new TextPrompt<string>("[grey]Enter resource name search (press Enter to show all):[/]")
                    .AllowEmpty());

            var filtered = string.IsNullOrWhiteSpace(searchTerm)
                ? resources
                : resources
                    .Where(r => r.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (filtered.Count == 0)
            {
                console.MarkupLine("[yellow]No matching resources found. Try another search.[/]");
                continue;
            }

            if (filtered.Count == 1)
            {
                var single = filtered[0];
                console.MarkupLine(
                    $"[grey]Using resource:[/] [blue]{Markup.Escape(single.Name)}[/] [grey](RG: {Markup.Escape(single.ResourceGroupName)})[/]");
                return single;
            }

            var choices = filtered.Select(SelectionChoice<ArmResourceInfo>.FromValue).ToList();
            choices.Add(SelectionChoice<ArmResourceInfo>.Cancel);

            var selected = console.Prompt(
                new SelectionPrompt<SelectionChoice<ArmResourceInfo>>()
                    .Title("[bold blue]Select a resource:[/]")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up and down to reveal more resources)[/]")
                    .UseConverter(c =>
                        c.IsCancel ? "[grey]← Cancel[/]" : FormatResourceChoice(c.Value!))
                    .AddChoices(choices));

            if (selected.IsCancel)
            {
                throw new OperationCanceledException("Resource selection cancelled.");
            }

            return selected.Value!;
        }
    }

    private static ResourceAccessLevel SelectAccessLevel(IAnsiConsole console, SupportedResourceType resourceType)
    {
        var choices = resourceType.AccessLevels
            .Select(SelectionChoice<ResourceAccessLevel>.FromValue)
            .ToList();

        choices.Add(SelectionChoice<ResourceAccessLevel>.Cancel);

        var selected = console.Prompt(
            new SelectionPrompt<SelectionChoice<ResourceAccessLevel>>()
                .Title("[bold blue]Select access level:[/]")
                .PageSize(10)
                .UseConverter(c =>
                    c.IsCancel
                        ? "[grey]← Cancel[/]"
                        : $"{Markup.Escape(c.Value!.Name)} [grey]- {Markup.Escape(c.Value.Description)}[/]")
                .AddChoices(choices));

        if (selected.IsCancel)
        {
            throw new OperationCanceledException("Access level selection cancelled.");
        }

        return selected.Value!;
    }

    private static void DisplayConfirmationSummary(
        IAnsiConsole console,
        TenantInfo tenant,
        SubscriptionInfo subscription,
        SupportedResourceType resourceType,
        ArmResourceInfo resource,
        ResourceAccessLevel accessLevel,
        PrincipalInfo principal,
        IReadOnlyList<string> currentRoles,
        IReadOnlyList<string> missingRoles)
    {
        console.WriteLine();
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
            .AddColumn(new TableColumn("[bold]Setting[/]").Width(24))
            .AddColumn(new TableColumn("[bold]Value[/]"));

        table.AddRow("Tenant", Markup.Escape(tenant.DisplayName));
        table.AddRow("Subscription", Markup.Escape(subscription.DisplayName));
        table.AddRow("Resource Type", Markup.Escape(resourceType.DisplayName));
        table.AddRow("Resource", Markup.Escape(resource.Name));
        table.AddRow("Resource Group", Markup.Escape(resource.ResourceGroupName));
        table.AddRow("Access Level", Markup.Escape(accessLevel.Name));
        table.AddRow("Principal", Markup.Escape(principal.ToString()));
        table.AddRow("Requested Roles", Markup.Escape(string.Join(", ", accessLevel.RoleNames)));

        if (currentRoles.Count == 0)
        {
            table.AddRow("Current Roles", "[grey]None at this scope[/]");
        }
        else
        {
            table.AddRow("Current Roles", Markup.Escape(string.Join(", ", currentRoles)));
        }

        if (missingRoles.Count == 0)
        {
            table.AddRow("Roles To Add", "[green]None (already compliant)[/]");
        }
        else
        {
            table.AddRow("Roles To Add", $"[yellow]{Markup.Escape(string.Join(", ", missingRoles))}[/]");
        }

        console.Write(table);
    }

    private static string FormatResourceChoice(ArmResourceInfo resource)
    {
        var kindSegment = string.IsNullOrWhiteSpace(resource.Kind)
            ? string.Empty
            : $" | Kind: {resource.Kind}";

        return $"{Markup.Escape(resource.Name)} [grey](RG: {Markup.Escape(resource.ResourceGroupName)} | {Markup.Escape(resource.Location)}{Markup.Escape(kindSegment)})[/]";
    }

    private sealed record SelectionChoice<T>(T? Value, bool IsCancel = false)
    {
        public static SelectionChoice<T> Cancel => new(default, true);
        public static SelectionChoice<T> FromValue(T value) => new(value, false);
    }
}
