using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace TestProject;

/// <summary>
/// Exercises a single HTTP-triggered function that also declares non-HTTP output bindings
/// (<c>[QueueOutput]</c> and <c>[BlobOutput]</c>) on a multi-output result type, alongside its
/// <c>[HttpResult]</c> response. See
/// https://github.com/bjorkstromm/azure-functions-test-framework/issues/142.
/// </summary>
public class HttpMixedOutputBindingFunction(IProcessedItemsService processedItems)
{
    [Function("CreateItemWithOutputs")]
    public async Task<CreateItemWithOutputsResult> CreateItemWithOutputs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items-with-outputs")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteStringAsync("created");

        var queueMessage = "queued:created";
        var blobContent = "blob:created";

        // Workaround for ASP.NET Core integration mode (ConfigureFunctionsWebApplication()), where
        // GetOutputData() cannot capture [QueueOutput]/[BlobOutput] because that mode has no
        // corresponding gRPC InvocationResponse round trip for HTTP-triggered invocations. Recording
        // through an injected service works identically in both modes, so tests can assert against it
        // regardless of which mode the host was started in.
        processedItems.Add($"QueueMessage:{queueMessage}");
        processedItems.Add($"BlobContent:{blobContent}");

        return new CreateItemWithOutputsResult
        {
            HttpResponse = response,
            QueueMessage = queueMessage,
            BlobContent = blobContent
        };
    }
}

public sealed class CreateItemWithOutputsResult
{
    [HttpResult]
    public HttpResponseData HttpResponse { get; set; } = default!;

    [QueueOutput("item-created-queue")]
    public string QueueMessage { get; set; } = string.Empty;

    [BlobOutput("item-created/latest.txt")]
    public string BlobContent { get; set; } = string.Empty;
}
