using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class McpToolTriggerFunction
{
    private readonly ILogger<McpToolTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public McpToolTriggerFunction(ILogger<McpToolTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("InvokeMcpTool")]
    public void Run([McpToolTrigger("InvokeMcpTool", "A simple MCP tool for testing")] ToolInvocationContext toolContext)
    {
        _logger.LogInformation("MCP tool invoked: {ToolName}", toolContext.Name);
        _processedItems.Add(toolContext.Name);
    }
}
