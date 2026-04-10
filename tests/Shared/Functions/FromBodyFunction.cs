using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
#if USE_ASPNET_CORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace TestProject;

/// <summary>
/// Functions that exercise the <c>[FromBody]</c> attribute for automatic JSON body deserialization.
/// </summary>
public class FromBodyFunction
{
    /// <summary>
    /// Accepts a <see cref="CreateItemRequest"/> via the <c>[FromBody]</c> attribute
    /// and returns it in the response body to verify deserialization worked.
    /// </summary>
    [Function("EchoFromBody")]
    public async Task<HttpResponseData> EchoFromBody(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "echo-from-body")] HttpRequestData req,
        [Microsoft.Azure.Functions.Worker.Http.FromBody] CreateItemRequest body)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(body);
        return response;
    }

#if USE_ASPNET_CORE
    /// <summary>
    /// ASP.NET Core variant using <c>[FromBody]</c>.
    /// </summary>
    [Function("EchoFromBodyAspNetCore")]
    public IActionResult EchoFromBodyAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "aspnetcore/echo-from-body")] HttpRequest req,
        [Microsoft.Azure.Functions.Worker.Http.FromBody] CreateItemRequest body)
        => new OkObjectResult(body);
#endif
}
