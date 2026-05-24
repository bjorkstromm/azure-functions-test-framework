using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class BlobTriggerFunction
{
    private readonly ILogger<BlobTriggerFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public BlobTriggerFunction(ILogger<BlobTriggerFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("ProcessBlob")]
    public void Run([BlobTrigger("test-container/{name}")] string content, string name)
    {
        _logger.LogInformation("Processing blob '{Name}': {Content}", name, content);
        _processedItems.Add($"{name}:{content}");
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Function("ProcessBlobClient")]
    public void RunBlobClient([BlobTrigger("test-container/{name}")] BlobClient client, string name)
    {
        _logger.LogInformation("Processing blob client '{Name}': container={Container}, blob={Blob}",
            name, client.BlobContainerName, client.Name);
        _processedItems.Add($"{client.BlobContainerName}/{client.Name}");
    }
}
