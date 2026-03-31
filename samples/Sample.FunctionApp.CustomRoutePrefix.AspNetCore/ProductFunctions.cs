using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Sample.FunctionApp.CustomRoutePrefix.AspNetCore;

/// <summary>
/// HTTP-triggered functions for the products catalogue.
/// All routes are served under the <c>v1</c> prefix configured in <c>host.json</c>.
/// </summary>
public class ProductFunctions
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductFunctions> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProductFunctions(IProductService productService, ILogger<ProductFunctions> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [Function("GetProducts")]
    public async Task<HttpResponseData> GetProducts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequestData req)
    {
        _logger.LogInformation("GetProducts called");

        var products = _productService.GetAll();
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(products, JsonOptions));
        return response;
    }

    [Function("GetProduct")]
    public async Task<HttpResponseData> GetProduct(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("GetProduct called for id={Id}", id);

        var product = _productService.GetById(id);
        if (product == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(product, JsonOptions));
        return response;
    }

    [Function("CreateProduct")]
    public async Task<HttpResponseData> CreateProduct(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequestData req)
    {
        _logger.LogInformation("CreateProduct called");

        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            badRequest.WriteString("Request body is required");
            return badRequest;
        }

        var input = JsonSerializer.Deserialize<CreateProductRequest>(body, JsonOptions);
        if (input == null || string.IsNullOrWhiteSpace(input.Name))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            badRequest.WriteString("Name is required");
            return badRequest;
        }

        var product = _productService.Create(input.Name, input.Price);
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(product, JsonOptions));
        return response;
    }

    [Function("DeleteProduct")]
    public HttpResponseData DeleteProduct(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "products/{id}")] HttpRequestData req,
        string id)
    {
        _logger.LogInformation("DeleteProduct called for id={Id}", id);

        var deleted = _productService.Delete(id);
        return req.CreateResponse(deleted ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
    }
    private sealed record CreateProductRequest(string Name, decimal Price);
}
