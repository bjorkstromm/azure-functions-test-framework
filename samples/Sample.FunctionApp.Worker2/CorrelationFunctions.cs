using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Sample.FunctionApp.Worker2;

public class CorrelationFunctions
{
    [Function("GetCorrelation")]
    public async Task<HttpResponseData> GetCorrelation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "correlation")] HttpRequestData req,
        FunctionContext context)
    {
        context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new CorrelationIdResponse
        {
            CorrelationId = correlationId as string
        });

        return response;
    }
}

public sealed class CorrelationIdResponse
{
    public string? CorrelationId { get; set; }
}
