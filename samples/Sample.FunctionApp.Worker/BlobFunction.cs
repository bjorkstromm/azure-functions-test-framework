using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Example blob-triggered function. In tests, invoke via
/// <c>host.InvokeBlobAsync("ProcessBlob", BinaryData.FromString("content"), "container/file.txt")</c>.
/// </summary>
public class BlobFunction
{
    private readonly ILogger<BlobFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public BlobFunction(ILogger<BlobFunction> logger, IProcessedItemsService processedItems)
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
