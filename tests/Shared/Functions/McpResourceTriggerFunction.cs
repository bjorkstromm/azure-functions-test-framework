using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class McpResourceTriggerFunction
{
    private readonly ILogger<McpResourceTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public McpResourceTriggerFunction(ILogger<McpResourceTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("GetMcpResource")]
    public void Run([McpResourceTrigger("resource://test/{name}", "GetMcpResource")] ResourceInvocationContext resourceContext)
    {
        _logger.LogInformation("MCP resource requested: {Uri}", resourceContext.Uri);
        _processedItems.Add(resourceContext.Uri ?? string.Empty);
    }
}
