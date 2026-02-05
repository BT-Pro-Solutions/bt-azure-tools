namespace BTAzureTools.Core.Abstractions;

/// <summary>
/// Registry for discovering and resolving tools.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    IReadOnlyList<ToolDescriptor> GetAllTools();
    
    /// <summary>
    /// Resolves a tool by its command name.
    /// </summary>
    ITool? ResolveByCommand(string command);
    
    /// <summary>
    /// Registers a tool type.
    /// </summary>
    void Register<TTool>() where TTool : ITool;
}
