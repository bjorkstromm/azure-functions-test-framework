using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace TestProject;

/// <summary>
/// Reproduces the examples carl-berg posted in
/// https://github.com/bjorkstromm/azure-functions-test-framework/issues/142#issuecomment-6035594598:
/// an HTTP-triggered function whose <c>[QueueOutput]</c> is applied directly to the method
/// (binding name <c>$return</c>), with no separate HTTP output binding at all.
/// </summary>
public class QueueOutputOnReturnValueFunction
{
    [Function("MyQueueFunction")]
    [QueueOutput("my-queue")]
    public async Task<string> ReceiveData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "some-route")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        await request.ReadAsStringAsync();
        return "Processed data";
    }

    [Function("MyBlobAndQueueFunction")]
    [QueueOutput("my-other-queue")]
    public async Task<string> ReceiveData2(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "some-other-route")] HttpRequestData request,
        [BlobInput("blob-container-name")] BlobContainerClient blobContainerClient,
        CancellationToken cancellationToken)
    {
        await request.ReadAsStringAsync();

        var blobName = "some-unique-name.json";
        await blobContainerClient.UploadBlobAsync(blobName, new BinaryData("Processed data"), cancellationToken);

        return "Reference to uploaded blob";
    }
}
