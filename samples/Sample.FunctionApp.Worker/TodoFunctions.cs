using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Sample.FunctionApp.Worker;

public class TodoFunctions
{
    private readonly ITodoService _todoService;

    public TodoFunctions(ITodoService todoService)
    {
        _todoService = todoService;
    }

    [Function("GetTodos")]
    public async Task<HttpResponseData> GetTodos(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")] HttpRequestData req)
    {
        var todos = await _todoService.GetAllAsync();
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(todos);
        return response;
    }

    [Function("GetTodo")]
    public async Task<HttpResponseData> GetTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var todo = await _todoService.GetByIdAsync(id);
        if (todo == null) return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(todo);
        return response;
    }

    [Function("CreateTodo")]
    public async Task<HttpResponseData> CreateTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequestData req)
    {
        var todo = await req.ReadFromJsonAsync<TodoItem>();
        if (todo == null || string.IsNullOrWhiteSpace(todo.Title))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Title is required");
            return badRequest;
        }

        var created = await _todoService.CreateAsync(todo);
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/todos/{created.Id}");
        await response.WriteAsJsonAsync(created);
        return response;
    }

    [Function("UpdateTodo")]
    public async Task<HttpResponseData> UpdateTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var updatedTodo = await req.ReadFromJsonAsync<TodoItem>();
        if (updatedTodo == null || string.IsNullOrWhiteSpace(updatedTodo.Title))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Title is required");
            return badRequest;
        }

        var existing = await _todoService.UpdateAsync(id, updatedTodo);
        if (existing == null) return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(existing);
        return response;
    }

    [Function("DeleteTodo")]
    public async Task<HttpResponseData> DeleteTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var deleted = await _todoService.DeleteAsync(id);
        if (!deleted) return req.CreateResponse(HttpStatusCode.NotFound);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Same as <see cref="GetTodo"/> but uses <c>request</c> instead of <c>req</c> as the HTTP
    /// trigger parameter name.  This verifies that the framework reads the actual binding name from
    /// function metadata rather than hardcoding "req".
    /// </summary>
    [Function("GetTodoAlt")]
    public async Task<HttpResponseData> GetTodoAlt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id}/alt")] HttpRequestData request,
        string id)
    {
        var todo = await _todoService.GetByIdAsync(id);
        if (todo == null) return request.CreateResponse(HttpStatusCode.NotFound);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(todo);
        return response;
    }

    /// <summary>
    /// Reads a todo by accessing <see cref="FunctionContext"/> as a direct function parameter
    /// (rather than via <c>req.FunctionContext</c>).  This verifies that <see cref="FunctionContext"/>
    /// is correctly injected by the worker runtime in the test framework.
    /// </summary>
    [Function("GetTodoWithContext")]
    public async Task<HttpResponseData> GetTodoWithContext(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id}/with-context")] HttpRequestData req,
        string id,
        FunctionContext context)
    {
        // Verify FunctionContext was injected
        if (context == null)
        {
            var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
            await bad.WriteStringAsync("FunctionContext was null");
            return bad;
        }

        var todo = await _todoService.GetByIdAsync(id);
        if (todo == null) return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(todo);
        return response;
    }

    /// <summary>
    /// Reads the todo ID exclusively from <see cref="FunctionContext.BindingContext"/>
    /// <c>.BindingData["id"]</c> rather than accepting it as a direct function parameter.
    /// This endpoint exists to verify that route parameters are present in <c>BindingData</c>.
    /// </summary>
    [Function("GetTodoByBindingData")]
    public async Task<HttpResponseData> GetTodoByBindingData(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id}/binding-data")] HttpRequestData req)
    {
        req.FunctionContext.BindingContext.BindingData.TryGetValue("id", out var idValue);
        var id = idValue as string;

        if (string.IsNullOrEmpty(id))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("'id' was not found in BindingContext.BindingData");
            return bad;
        }

        var todo = await _todoService.GetByIdAsync(id);
        if (todo == null) return req.CreateResponse(HttpStatusCode.NotFound);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(todo);
        return response;
    }
}

public class TodoItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
