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
public class HttpMixedOutputBindingFunction
{
    [Function("CreateItemWithOutputs")]
    public CreateItemWithOutputsResult CreateItemWithOutputs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items-with-outputs")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.WriteString("created");

        return new CreateItemWithOutputsResult
        {
            HttpResponse = response,
            QueueMessage = "queued:created",
            BlobContent = "blob:created"
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
