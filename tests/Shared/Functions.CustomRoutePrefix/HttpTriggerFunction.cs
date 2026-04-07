using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
#if USE_ASPNET_CORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace TestProject.CustomRoutePrefix;

public class HttpTriggerFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IItemService _itemService;

    public HttpTriggerFunction(IItemService itemService) => _itemService = itemService;

    [Function("GetItemsCrp")]
    public HttpResponseData GetItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        response.WriteString(JsonSerializer.Serialize(_itemService.GetAll(), JsonOptions));
        return response;
    }

    [Function("GetItemCrp")]
    public HttpResponseData GetItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}")] HttpRequestData req,
        string id)
    {
        var item = _itemService.GetById(id);
        if (item == null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        response.WriteString(JsonSerializer.Serialize(item, JsonOptions));
        return response;
    }

    [Function("CreateItemCrp")]
    public async Task<HttpResponseData> CreateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            bad.WriteString("Request body is required");
            return bad;
        }
        var input = JsonSerializer.Deserialize<CreateItemCrpRequest>(body, JsonOptions);
        if (input == null || string.IsNullOrWhiteSpace(input.Name))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            bad.WriteString("Name is required");
            return bad;
        }
        var item = _itemService.Create(input.Name);
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Content-Type", "application/json");
        response.WriteString(JsonSerializer.Serialize(item, JsonOptions));
        return response;
    }

    [Function("DeleteItemCrp")]
    public HttpResponseData DeleteItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "items/{id}")] HttpRequestData req,
        string id)
    {
        var deleted = _itemService.Delete(id);
        return req.CreateResponse(deleted ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
    }

#if USE_ASPNET_CORE
    [Function("GetItemsAspNetCoreCrp")]
    public IActionResult GetItemsAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/items")] HttpRequest req)
        => new OkObjectResult(_itemService.GetAll());

    [Function("GetItemAspNetCoreCrp")]
    public IActionResult GetItemAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/items/{id}")] HttpRequest req,
        string id)
    {
        var item = _itemService.GetById(id);
        return item == null ? new NotFoundResult() : new OkObjectResult(item);
    }

    [Function("CreateItemAspNetCoreCrp")]
    public async Task<IActionResult> CreateItemAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "aspnetcore/items")] HttpRequest req)
    {
        var input = await req.ReadFromJsonAsync<CreateItemCrpRequest>();
        if (input == null || string.IsNullOrWhiteSpace(input.Name))
            return new BadRequestObjectResult("Name is required");
        var item = _itemService.Create(input.Name);
        return new CreatedResult($"/v1/aspnetcore/items/{item.Id}", item);
    }
#endif
}

public sealed record CreateItemCrpRequest(string Name);
