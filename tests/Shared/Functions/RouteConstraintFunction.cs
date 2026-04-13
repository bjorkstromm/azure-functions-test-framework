using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace TestProject;

/// <summary>
/// HTTP trigger functions that exercise route constraints, optional parameters, and
/// catch-all segments.  These functions use <see cref="HttpRequestData"/> so they work
/// in all four test flavours (direct gRPC and ASP.NET Core integration, IHostBuilder and
/// FunctionsApplicationBuilder).
/// </summary>
public static class RouteConstraintFunction
{
    // ── Int constraint ────────────────────────────────────────────────────────

    /// <summary>Route: items-constrained/{id:int} — only matches integer path segments.</summary>
    [Function("GetConstrainedByInt")]
    public static async Task<HttpResponseData> GetConstrainedByInt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items-constrained/{id:int}")]
        HttpRequestData req,
        string id)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new RouteEchoResponse("int", id));
        return response;
    }

    // ── Alpha constraint ──────────────────────────────────────────────────────

    /// <summary>Route: items-constrained/{name:alpha} — only matches all-letter path segments.
    /// Together with <see cref="GetConstrainedByInt"/> this exercises best-match selection:
    /// a numeric value routes to int, an alphabetic value routes to alpha.</summary>
    [Function("GetConstrainedByAlpha")]
    public static async Task<HttpResponseData> GetConstrainedByAlpha(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items-constrained/{name:alpha}")]
        HttpRequestData req,
        string name)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new RouteEchoResponse("alpha", name));
        return response;
    }

    // ── Optional parameter ────────────────────────────────────────────────────

    /// <summary>Route: items-optional/{page?} — the page segment is optional.</summary>
    [Function("GetOptionalPage")]
    public static async Task<HttpResponseData> GetOptionalPage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items-optional/{page?}")]
        HttpRequestData req)
    {
        // Read the optional route value via BindingData so this works in both gRPC and
        // ASP.NET Core integration modes without depending on optional parameter binding.
        var page = GetRouteValue(req, "page");
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new OptionalPageResponse(page));
        return response;
    }

    // ── Catch-all parameter ───────────────────────────────────────────────────

    /// <summary>Route: files/{*rest} — catch-all consumes all remaining path segments.</summary>
    [Function("GetCatchAllFiles")]
    public static async Task<HttpResponseData> GetCatchAllFiles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "files/{*rest}")]
        HttpRequestData req)
    {
        var rest = GetRouteValue(req, "rest") ?? string.Empty;
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new CatchAllResponse(rest));
        return response;
    }

    // ── Combined int+range constraint ─────────────────────────────────────────

    /// <summary>Route: items-range/{id:int:min(1):max(100)} — combines type and range constraints.</summary>
    [Function("GetRangedItem")]
    public static async Task<HttpResponseData> GetRangedItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items-range/{id:int:min(1):max(100)}")]
        HttpRequestData req,
        string id)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new RouteEchoResponse("ranged-int", id));
        return response;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a route parameter value from <see cref="BindingContext.BindingData"/>.
    /// Works in both gRPC-direct and ASP.NET Core integration modes.
    /// </summary>
    private static string? GetRouteValue(HttpRequestData req, string paramName)
    {
        req.FunctionContext.BindingContext.BindingData.TryGetValue(paramName, out var value);
        return value as string;
    }
}

// ── Response types ────────────────────────────────────────────────────────────

/// <summary>Response for constraint echo endpoints.</summary>
public sealed record RouteEchoResponse(string Type, string Value);

/// <summary>Response for the optional-page endpoint.</summary>
public sealed record OptionalPageResponse(string? Page);

/// <summary>Response for the catch-all files endpoint.</summary>
public sealed record CatchAllResponse(string Rest);
