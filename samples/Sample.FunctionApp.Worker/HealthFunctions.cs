using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace Sample.FunctionApp.Worker;

public class HealthFunctions
{
    private readonly IConfiguration _configuration;

    public HealthFunctions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Function("Health")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { status = "Healthy" });
        return response;
    }

    /// <summary>
    /// Echoes the inbound HTTP method in <c>X-Probe-Method</c> for integration tests that verify
    /// GET, HEAD, OPTIONS, and PATCH route through the test host. For PATCH, the request body is
    /// repeated in <c>X-Probe-Request-Body</c> so callers can assert the payload reached the function.
    /// </summary>
    [Function("HttpVerbsProbe")]
    public async Task<HttpResponseData> HttpVerbsProbe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", "options", "patch", Route = "http-verbs-probe")]
        HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("X-Probe-Method", req.Method);
        if (string.Equals(req.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            var requestBody = await req.ReadAsStringAsync();
            response.Headers.Add("X-Probe-Request-Body", requestBody ?? string.Empty);
        }

        if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(req.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            await response.WriteStringAsync("probe");
        }

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

    [Function("GetConfigurationValue")]
    public async Task<HttpResponseData> GetConfigurationValue(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "config/{key}")] HttpRequestData req,
        string key)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ConfigurationValueResponse
        {
            Key = key,
            Value = _configuration[key]
        });

        return response;
    }
}

public sealed class ConfigurationValueResponse
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}
