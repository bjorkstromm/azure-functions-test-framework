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
#if USE_ASPNET_CORE
    [Function("CreateItemWithAspNetCoreOutputs")]
    public CreateItemWithAspNetCoreOutputsResult CreateItemWithAspNetCoreOutputs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "aspnetcore/items-with-outputs")]
        Microsoft.AspNetCore.Http.HttpRequest req) =>
        new()
        {
            HttpResponse = new Microsoft.AspNetCore.Mvc.CreatedResult("/api/items", "created"),
            QueueMessage = "queued:created",
            BlobContent = "blob:created"
        };
#endif

    [Function("CreateItemWithOutputs")]
    public async Task<CreateItemWithOutputsResult> CreateItemWithOutputs(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items-with-outputs")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteStringAsync("created");

        var value = await req.ReadAsStringAsync();
        if (string.IsNullOrEmpty(value))
        {
            value = "created";
        }
        var queueMessage = $"queued:{value}";
        var blobContent = $"blob:{value}";

        return new CreateItemWithOutputsResult
        {
            HttpResponse = response,
            QueueMessage = queueMessage,
            BlobContent = blobContent
        };
    }
}

#if USE_ASPNET_CORE
public sealed class CreateItemWithAspNetCoreOutputsResult
{
    [HttpResult]
    public Microsoft.AspNetCore.Mvc.IActionResult HttpResponse { get; set; } = default!;

    [QueueOutput("item-created-queue")]
    public string QueueMessage { get; set; } = string.Empty;

    [BlobOutput("item-created/latest.txt")]
    public string BlobContent { get; set; } = string.Empty;
}
#endif

public sealed class CreateItemWithOutputsResult
{
    [HttpResult]
    public HttpResponseData HttpResponse { get; set; } = default!;

    [QueueOutput("item-created-queue")]
    public string QueueMessage { get; set; } = string.Empty;

    [BlobOutput("item-created/latest.txt")]
    public string BlobContent { get; set; } = string.Empty;
}
