using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class BlobTriggerFunction
{
    private readonly ILogger<BlobTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public BlobTriggerFunction(ILogger<BlobTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    [Function("ProcessBlob")]
    public void Run([BlobTrigger("test-container/{name}")] string content, string name)
    {
        _logger.LogInformation("Processing blob '{Name}': {Content}", name, content);
        _processedItems.Add($"{name}:{content}");
    }
}
