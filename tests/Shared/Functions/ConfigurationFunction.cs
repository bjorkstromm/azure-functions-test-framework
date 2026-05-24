using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System.Net;
#if USE_ASPNET_CORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class ConfigurationFunction
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public ConfigurationFunction(IConfiguration configuration) => _configuration = configuration;

    /// <summary>
    /// Represents this member.
    /// </summary>
    [Function("GetConfigValue")]
    public async Task<HttpResponseData> GetConfigValue(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "config/{key}")] HttpRequestData req,
        string key)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ConfigValueResponse(key, _configuration[key]));
        return response;
    }

    /// <summary>
    /// Represents this member.
    /// </summary>
    [Function("Health")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { status = "Healthy" });
        return response;
    }

#if USE_ASPNET_CORE
    /// <summary>
    /// Represents this member.
    /// </summary>
    [Function("GetConfigValueAspNetCore")]
    public IActionResult GetConfigValueAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/config/{key}")] HttpRequest req,
        string key)
        => new OkObjectResult(new ConfigValueResponse(key, _configuration[key]));

    /// <summary>
    /// Represents this member.
    /// </summary>
    [Function("HealthAspNetCore")]
    public IActionResult HealthAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/health")] HttpRequest req)
        => new OkObjectResult(new { status = "Healthy" });
#endif
}

/// <summary>
/// Represents this type.
/// </summary>
public sealed record ConfigValueResponse(string Key, string? Value);
