using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class McpPromptTriggerFunction
{
    private readonly ILogger<McpPromptTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public McpPromptTriggerFunction(ILogger<McpPromptTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("InvokeMcpPrompt")]
    public void Run([McpPromptTrigger("InvokeMcpPrompt")] PromptInvocationContext promptContext)
    {
        _logger.LogInformation("MCP prompt invoked: {PromptName}", promptContext.Name);
        _processedItems.Add(promptContext.Name);
    }
}
