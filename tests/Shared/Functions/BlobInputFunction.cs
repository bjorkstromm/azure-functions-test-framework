using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TestProject;

public class BlobInputFunction
{
    private readonly ILogger<BlobInputFunction> _logger;
    private readonly IProcessedItemsService _processedItems;

    public BlobInputFunction(ILogger<BlobInputFunction> logger, IProcessedItemsService processedItems)
    {
        _logger = logger;
        _processedItems = processedItems;
    }

    [Function("ReadBlobInput")]
    public void Run(
        [QueueTrigger("read-blob-queue")] string unused,
        [BlobInput("test-input/data.txt")] string blobContent)
    {
        _logger.LogInformation("Read blob input content: {Content}", blobContent);
        _processedItems.Add(blobContent);
    }

    [Function("ReadBlobInputClient")]
    public void RunBlobClient(
        [QueueTrigger("read-blob-client-queue")] string unused,
        [BlobInput("test-input-client/data.txt")] BlobClient client)
    {
        _logger.LogInformation("Read blob input client: container={Container}, blob={Blob}",
            client.BlobContainerName, client.Name);
        _processedItems.Add($"{client.BlobContainerName}/{client.Name}");
    }
}
