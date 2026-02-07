using BTAzureTools.Core.Abstractions;
using Spectre.Console;

namespace BTAzureTools.Cli;

/// <summary>
/// Main menu for selecting and running tools.
/// </summary>
public sealed class ToolMenu
{
    private readonly IToolRegistry _toolRegistry;
    
    public ToolMenu(IToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }
    
    /// <summary>
    /// Displays the main menu and runs the selected tool.
    /// </summary>
    public async Task<int> RunAsync(IAnsiConsole console, CancellationToken cancellationToken)
    {
        console.Write(new FigletText("BT Azure Tools")
            .Color(Color.Blue));
        
        console.MarkupLine("[grey]A collection of tools for common Azure tasks[/]");
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
    
    private sealed record ToolChoice(ToolDescriptor? Descriptor)
    {
        public string DisplayText => Descriptor is null 
            ? "[grey]Exit[/]" 
            : $"{Descriptor.Name} - [grey]{Descriptor.Description}[/]";
    }
}
