using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
#if USE_ASPNET_CORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace TestProject;

public class HttpVerbsFunction
{
    private const int MaxEchoChars = 4096;

    [Function("HttpVerbsProbe")]
    public async Task<HttpResponseData> HttpVerbsProbe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", "options", "patch", Route = "http-verbs-probe")]
        HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("X-Probe-Method", req.Method);
        if (string.Equals(req.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
        {
            var body = await req.ReadAsStringAsync() ?? string.Empty;
            if (body.Length > MaxEchoChars) body = body[..MaxEchoChars];
            await response.WriteStringAsync(body);
            return response;
        }
        if (string.Equals(req.Method, "GET", StringComparison.OrdinalIgnoreCase))
            await response.WriteStringAsync("probe");
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

#if USE_ASPNET_CORE
    [Function("HttpVerbsProbeAspNetCore")]
    public async Task<IActionResult> HttpVerbsProbeAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "head", "options", "patch", Route = "aspnetcore/http-verbs-probe")]
        HttpRequest req)
    {
        req.HttpContext.Response.Headers.Append("X-Probe-Method", req.Method);
        if (req.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new System.IO.StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();
            if (body.Length > MaxEchoChars) body = body[..MaxEchoChars];
            return new ContentResult { Content = body, ContentType = "text/plain", StatusCode = 200 };
        }
        if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            return new ContentResult { Content = "probe", ContentType = "text/plain", StatusCode = 200 };
        return new OkResult();
    }

    [Function("EchoAspNetCore")]
    public async Task<IActionResult> EchoAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "aspnetcore/echo")] HttpRequest req)
    {
        using var reader = new System.IO.StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();
        return new ContentResult { Content = body, ContentType = "text/plain", StatusCode = 200 };
    }
#endif
}
