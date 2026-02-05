using BTAzureTools.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace BTAzureTools.Cli;

/// <summary>
/// Registry for discovering and resolving tools.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _toolTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToolDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    
    public ToolRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public void Register<TTool>() where TTool : ITool
    {
        var toolType = typeof(TTool);
        
        // Get the Descriptor property value - it's an instance property, so we need to create an instance
        // Use the service provider to properly resolve all dependencies
        var tool = _serviceProvider.GetRequiredService<TTool>();
        var descriptor = tool.Descriptor;
        
        _toolTypes[descriptor.Command] = toolType;
        _descriptors[descriptor.Command] = descriptor;
    }
    
    public IReadOnlyList<ToolDescriptor> GetAllTools()
    {
        return _descriptors.Values.OrderBy(t => t.Name).ToList();
    }
    
    public ITool? ResolveByCommand(string command)
    {
        if (!_toolTypes.TryGetValue(command, out var toolType))
            return null;
        
        return (ITool)_serviceProvider.GetRequiredService(toolType);
    }
}
