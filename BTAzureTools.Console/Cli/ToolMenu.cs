using BTAzureTools.Core.Abstractions;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics;

namespace BTAzureTools.Cli;

/// <summary>
/// Main menu for selecting and running tools.
/// </summary>
public sealed class ToolMenu
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ICredentialProvider _credentialProvider;
    
    public ToolMenu(
        IToolRegistry toolRegistry,
        ICredentialProvider credentialProvider)
    {
        _toolRegistry = toolRegistry;
        _credentialProvider = credentialProvider;
    }
    
    /// <summary>
    /// Displays the main menu and runs the selected tool.
    /// </summary>
    public async Task<int> RunAsync(IAnsiConsole console, CancellationToken cancellationToken)
    {
        console.Write(new FigletText("BT Azure Tools")
            .Color(Color.Blue));
        
        console.MarkupLine("[grey]A collection of tools for common Azure tasks[/]");
        await DisplayCurrentUserAsync(console, cancellationToken);
        console.WriteLine();
        
        var tools = _toolRegistry.GetAllTools();
        
        if (tools.Count == 0)
        {
            console.MarkupLine("[red]No tools are registered.[/]");
            return 1;
        }
        
        // Create menu with tool names and descriptions
        var choices = tools.Select(t => new ToolChoice(t)).ToList();
        choices.Add(new ToolChoice(null)); // Exit option
        
        var selected = console.Prompt(
            new SelectionPrompt<ToolChoice>()
                .Title("[bold blue]Select a tool to run:[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Blue, decoration: Decoration.Bold))
                .UseConverter(c => c.DisplayText)
                .AddChoices(choices));
        
        if (selected.Descriptor is null)
        {
            console.MarkupLine("[grey]Goodbye![/]");
            return 0;
        }
        
        console.WriteLine();
        console.Write(new Rule($"[blue]{selected.Descriptor.Name}[/]"));
        console.WriteLine();
        
        var tool = _toolRegistry.ResolveByCommand(selected.Descriptor.Command);
        if (tool is null)
        {
            console.MarkupLine("[red]Failed to resolve tool.[/]");
            return 1;
        }
        
        var context = new ToolExecutionContext(
            Guid.NewGuid(),
            console,
            cancellationToken);
        
        try
        {
            return await tool.ExecuteAsync(context);
        }
        catch (OperationCanceledException)
        {
            console.MarkupLine("[yellow]Operation cancelled.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 1;
        }
    }

    private async Task DisplayCurrentUserAsync(IAnsiConsole console, CancellationToken cancellationToken)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync(console, cancellationToken);
            DisplayCurrentUser(console, currentUser);

            if (TryDecodeGuestUpnToExternalEmail(currentUser.UserPrincipalName) is { } externalEmail)
            {
                console.MarkupLine($"[yellow]Guest context detected:[/] [grey]source email likely[/] [blue]{Markup.Escape(externalEmail)}[/]");

                if (console.Confirm("Run Azure CLI login now to switch account? (az login)", false))
                {
                    console.WriteLine();
                    console.MarkupLine("[grey]Starting Azure CLI login...[/]");

                    var loginSucceeded = await RunAzLoginAsync(console, cancellationToken);
                    console.WriteLine();

                    if (loginSucceeded)
                    {
                        var refreshedUser = await GetCurrentUserAsync(console, cancellationToken);
                        DisplayCurrentUser(console, refreshedUser);

                        if (TryDecodeGuestUpnToExternalEmail(refreshedUser.UserPrincipalName) is { } refreshedExternalEmail)
                        {
                            console.MarkupLine($"[yellow]Still in guest context:[/] [grey]source email likely[/] [blue]{Markup.Escape(refreshedExternalEmail)}[/]");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            console.MarkupLine("[yellow]Could not determine signed-in Azure user.[/]");
        }
    }

    private async Task<(Guid ObjectId, string DisplayName, string? UserPrincipalName)> GetCurrentUserAsync(
        IAnsiConsole console,
        CancellationToken cancellationToken)
    {
        return await console.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Checking signed-in Azure user...", async _ =>
                await _credentialProvider.GetCurrentUserAsync(cancellationToken));
    }

    private static void DisplayCurrentUser(
        IAnsiConsole console,
        (Guid ObjectId, string DisplayName, string? UserPrincipalName) currentUser)
    {
        var userDisplay = string.IsNullOrWhiteSpace(currentUser.UserPrincipalName)
            ? currentUser.DisplayName
            : $"{currentUser.DisplayName} ({currentUser.UserPrincipalName})";

        console.MarkupLine($"[grey]Logged in as:[/] [blue]{Markup.Escape(userDisplay)}[/]");
    }

    private static async Task<bool> RunAzLoginAsync(IAnsiConsole console, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "az",
                    Arguments = "login",
                    UseShellExecute = false
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                console.MarkupLine("[green]Azure CLI login completed.[/]");
                return true;
            }

            console.MarkupLine($"[yellow]Azure CLI login exited with code {process.ExitCode}.[/]");
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Win32Exception)
        {
            console.MarkupLine("[red]Azure CLI ('az') was not found on PATH.[/]");
            return false;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Failed to run az login:[/] {Markup.Escape(ex.Message)}");
            return false;
        }
    }
    
    private sealed record ToolChoice(ToolDescriptor? Descriptor)
    {
        public string DisplayText => Descriptor is null 
            ? "[grey]Exit[/]" 
            : $"{Descriptor.Name} - [grey]{Descriptor.Description}[/]";
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
}
