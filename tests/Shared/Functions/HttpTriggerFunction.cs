using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
#if USE_ASPNET_CORE
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
#endif

namespace TestProject;

public class HttpTriggerFunction
{
    private readonly IItemService _itemService;

    public HttpTriggerFunction(IItemService itemService) => _itemService = itemService;

    // ── HttpRequestData (all 4 flavors) ──────────────────────────────────────

    [Function("GetItems")]
    public async Task<HttpResponseData> GetItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(_itemService.GetAll());
        return response;
    }

    [Function("GetItem")]
    public async Task<HttpResponseData> GetItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}")] HttpRequestData req,
        string id)
    {
        var item = _itemService.GetById(id);
        if (item == null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(item);
        return response;
    }

    [Function("CreateItem")]
    public async Task<HttpResponseData> CreateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "items")] HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<CreateItemRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Name is required");
            return bad;
        }

        var item = _itemService.Create(body.Name);
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/items/{item.Id}");
        await response.WriteAsJsonAsync(item);
        return response;
    }

    [Function("UpdateItem")]
    public async Task<HttpResponseData> UpdateItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "items/{id}")] HttpRequestData req,
        string id)
    {
        var body = await req.ReadFromJsonAsync<UpdateItemRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Name is required");
            return bad;
        }

        var item = _itemService.Update(id, body.Name, body.IsCompleted);
        if (item == null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(item);
        return response;
    }

    [Function("DeleteItem")]
    public HttpResponseData DeleteItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "items/{id}")] HttpRequestData req,
        string id)
    {
        var deleted = _itemService.Delete(id);
        return req.CreateResponse(deleted ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Uses <c>request</c> instead of <c>req</c> as the binding parameter name.
    /// Verifies the framework reads the actual binding name from metadata rather than hardcoding "req".
    /// </summary>
    [Function("GetItemAlt")]
    public async Task<HttpResponseData> GetItemAlt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}/alt")] HttpRequestData request,
        string id)
    {
        var item = _itemService.GetById(id);
        if (item == null) return request.CreateResponse(HttpStatusCode.NotFound);
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(item);
        return response;
    }

    /// <summary>
    /// Reads the item ID from <see cref="FunctionContext.BindingContext"/>.BindingData["id"].
    /// Verifies route parameters are available in BindingData.
    /// </summary>
    [Function("GetItemByBindingData")]
    public async Task<HttpResponseData> GetItemByBindingData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}/binding-data")] HttpRequestData req)
    {
        req.FunctionContext.BindingContext.BindingData.TryGetValue("id", out var idValue);
        var id = idValue as string;
        if (string.IsNullOrEmpty(id))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("'id' not found in BindingData");
            return bad;
        }

        var item = _itemService.GetById(id);
        if (item == null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(item);
        return response;
    }

    /// <summary>
    /// Accepts <see cref="FunctionContext"/> as a direct parameter to verify injection.
    /// </summary>
    [Function("GetItemWithContext")]
    public async Task<HttpResponseData> GetItemWithContext(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "items/{id}/with-context")] HttpRequestData req,
        string id,
        FunctionContext context)
    {
        if (context == null)
        {
            var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
            await bad.WriteStringAsync("FunctionContext was null");
            return bad;
        }

        var item = _itemService.GetById(id);
        if (item == null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(item);
        return response;
    }

#if USE_ASPNET_CORE
    // ── HttpRequest (ASP.NET Core flavors only) ───────────────────────────────

    [Function("GetItemsAspNetCore")]
    public IActionResult GetItemsAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/items")] HttpRequest req)
        => new OkObjectResult(_itemService.GetAll());

    [Function("GetItemAspNetCore")]
    public IActionResult GetItemAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/items/{id}")] HttpRequest req,
        string id)
    {
        var item = _itemService.GetById(id);
        return item == null ? new NotFoundResult() : new OkObjectResult(item);
    }

    [Function("GetItemByGuidAspNetCore")]
    public IActionResult GetItemByGuidAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/items/by-guid/{itemId:guid}")]
        HttpRequest req,
        FunctionContext context,
        Guid itemId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var item = _itemService.GetById(itemId.ToString());
        return item == null ? new NotFoundResult() : new OkObjectResult(item);
    }

    [Function("CreateItemAspNetCore")]
    public async Task<IActionResult> CreateItemAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "aspnetcore/items")] HttpRequest req)
    {
        var body = await req.ReadFromJsonAsync<CreateItemRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
            return new BadRequestObjectResult("Name is required");

        var item = _itemService.Create(body.Name);
        return new CreatedAtRouteResult("GetItemAspNetCore", new { id = item.Id }, item);
    }

    [Function("UpdateItemAspNetCore")]
    public async Task<IActionResult> UpdateItemAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "aspnetcore/items/{id}")] HttpRequest req,
        string id)
    {
        var body = await req.ReadFromJsonAsync<UpdateItemRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Name))
            return new BadRequestObjectResult("Name is required");

        var item = _itemService.Update(id, body.Name, body.IsCompleted);
        return item == null ? new NotFoundResult() : new OkObjectResult(item);
    }

    [Function("DeleteItemAspNetCore")]
    public IActionResult DeleteItemAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "aspnetcore/items/{id}")] HttpRequest req,
        string id)
    {
        var deleted = _itemService.Delete(id);
        return deleted ? new NoContentResult() : new NotFoundResult();
    }
#endif
}

public sealed record CreateItemRequest(string Name);
public sealed record UpdateItemRequest(string Name, bool IsCompleted);
