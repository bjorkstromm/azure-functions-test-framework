using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Example blob-triggered function. In tests, invoke via
/// <c>host.InvokeBlobAsync("ProcessBlob", BinaryData.FromString("content"), "file.txt")</c>.
/// <para>
/// This function uses <c>string</c> as the blob parameter type, which works for text blobs
/// (the worker UTF-8 decodes the blob bytes).  For binary content use <c>byte[]</c> or
/// <c>BinaryData</c> instead.
/// </para>
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
