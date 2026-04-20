using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace TestProject;

/// <summary>
/// Function that echoes <see cref="FunctionContext.BindingContext"/>.<see cref="Microsoft.Azure.Functions.Worker.Http.HttpRequestData.FunctionContext"/>
/// <c>BindingData</c> back as JSON so tests can verify the test framework
/// populates it with the same keys/values as the real Azure Functions host.
/// </summary>
public class BindingDataFunction
{
    /// <summary>
    /// POST /api/echo-binding-data
    /// Returns the non-system entries of <c>BindingContext.BindingData</c> as a
    /// <c>Dictionary&lt;string, string?&gt;</c> JSON object.
    /// </summary>
    [Function("EchoBindingData")]
    public async Task<HttpResponseData> EchoBindingData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "echo-binding-data")]
        HttpRequestData req)
    {
        // Exclude internal sys.* keys; keep everything else (Headers, Query, body properties).
        var bindingData = req.FunctionContext.BindingContext.BindingData
            .Where(kv => !kv.Key.StartsWith("sys.", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(bindingData);
        return response;
    }
}
