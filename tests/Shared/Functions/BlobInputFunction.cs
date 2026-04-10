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
}
