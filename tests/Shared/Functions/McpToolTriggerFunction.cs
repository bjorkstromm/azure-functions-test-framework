using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class McpToolTriggerFunction
{
    private readonly ILogger<McpToolTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public McpToolTriggerFunction(ILogger<McpToolTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    [Function("InvokeMcpTool")]
    public void Run([McpToolTrigger("InvokeMcpTool", "A simple MCP tool for testing")] ToolInvocationContext toolContext)
    {
        _logger.LogInformation("MCP tool invoked: {ToolName}", toolContext.Name);
        _processedItems.Add(toolContext.Name);
    }
}
