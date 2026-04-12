using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
#if USE_ASPNET_CORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace TestProject;

/// <summary>
/// Functions that verify the test framework works correctly when the worker uses a custom JSON serializer.
/// </summary>
public class CustomSerializerFunction
{
    /// <summary>
    /// Accepts a <see cref="ProductRequest"/> via the HTTP request body and returns a
    /// <see cref="ProductResponse"/>. Both <c>ReadFromJsonAsync</c> and <c>WriteAsJsonAsync</c>
    /// use the worker's configured <c>WorkerOptions.Serializer</c>, so this endpoint verifies
    /// that a custom naming policy (e.g. snake_case) is applied end-to-end.
    /// </summary>
    [Function("EchoProduct")]
    public async Task<HttpResponseData> EchoProduct(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "echo-product")] HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<ProductRequest>();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ProductResponse
        {
            ProductId = body?.ProductId ?? string.Empty,
            ProductName = body?.ProductName ?? string.Empty,
            IsActive = body?.IsActive ?? false,
        });
        return response;
    }

#if USE_ASPNET_CORE
    /// <summary>ASP.NET Core variant — uses ASP.NET Core model binding and <c>OkObjectResult</c>.</summary>
    [Function("EchoProductAspNetCore")]
    public async Task<IActionResult> EchoProductAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "aspnetcore/echo-product")] HttpRequest req)
    {
        var body = await req.ReadFromJsonAsync<ProductRequest>();
        return new OkObjectResult(new ProductResponse
        {
            ProductId = body?.ProductId ?? string.Empty,
            ProductName = body?.ProductName ?? string.Empty,
            IsActive = body?.IsActive ?? false,
        });
    }
#endif
}

/// <summary>Input DTO for the custom-serializer echo endpoints.</summary>
public sealed class ProductRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>Output DTO for the custom-serializer echo endpoints.</summary>
public sealed class ProductResponse
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
