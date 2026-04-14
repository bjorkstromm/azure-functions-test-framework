using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class McpPromptTriggerFunction
{
    private readonly ILogger<McpPromptTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public McpPromptTriggerFunction(ILogger<McpPromptTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    [Function("InvokeMcpPrompt")]
    public void Run([McpPromptTrigger("InvokeMcpPrompt")] PromptInvocationContext promptContext)
    {
        _logger.LogInformation("MCP prompt invoked: {PromptName}", promptContext.Name);
        _processedItems.Add(promptContext.Name);
    }
}
