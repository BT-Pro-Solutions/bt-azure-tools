using Spectre.Console;

namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Describes a tool that can be executed.
/// </summary>
public sealed record ToolDescriptor(
    string Name,
    string Command,
    string Description);

/// <summary>
/// Context passed to tools during execution.
/// </summary>
public sealed record ToolExecutionContext(
    Guid RunId,
    IAnsiConsole Console,
    CancellationToken CancellationToken);

/// <summary>
/// Interface for tools that can be executed.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the tool descriptor.
    /// </summary>
    ToolDescriptor Descriptor { get; }
    
    /// <summary>
    /// Executes the tool.
    /// </summary>
    /// <returns>Exit code (0 for success).</returns>
    Task<int> ExecuteAsync(ToolExecutionContext context);
}
