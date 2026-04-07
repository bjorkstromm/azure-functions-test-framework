using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
#if USE_ASPNET_CORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace TestProject;

public class MiddlewareFunction
{
    [Function("GetCorrelation")]
    public async Task<HttpResponseData> GetCorrelation(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "correlation")] HttpRequestData req,
        FunctionContext context)
    {
        context.Items.TryGetValue(CorrelationMiddleware.ItemKey, out var correlationId);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new CorrelationIdResponse(correlationId as string));
        return response;
    }

#if USE_ASPNET_CORE
    [Function("GetCorrelationAspNetCore")]
    public IActionResult GetCorrelationAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/correlation")] HttpRequest req,
        FunctionContext context)
    {
        context.Items.TryGetValue(CorrelationMiddleware.ItemKey, out var correlationId);
        return new OkObjectResult(new CorrelationIdResponse(correlationId as string));
    }
#endif
}

public sealed record CorrelationIdResponse(string? CorrelationId);
