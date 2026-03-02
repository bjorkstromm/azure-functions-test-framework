using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Sample.FunctionApp;

public class HealthFunctions
{
    [Function("Health")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { status = "Healthy" });
        return response;
    }

    [Function("Echo")]
    public async Task<HttpResponseData> Echo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "echo")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain");
        await response.WriteStringAsync(body ?? string.Empty);
        return response;
    }
}
