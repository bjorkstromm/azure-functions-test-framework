using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Represents this type.
/// </summary>
public class CorrelationFunctions
{
    /// <summary>
    /// Represents this member.
    /// </summary>
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

/// <summary>
/// Represents this type.
/// </summary>
public sealed class CorrelationIdResponse
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? CorrelationId { get; set; }
}
