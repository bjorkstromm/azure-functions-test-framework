using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Sample.FunctionApp;

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
        await response.WriteAsJsonAsync(todos, HttpStatusCode.OK);
        return response;
    }

    [Function("GetTodo")]
    public async Task<HttpResponseData> GetTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var todo = await _todoService.GetByIdAsync(id);

        if (todo == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            return notFoundResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(todo, HttpStatusCode.OK);
        return response;
    }

    [Function("CreateTodo")]
    public async Task<HttpResponseData> CreateTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequestData req)
    {
        var todo = await req.ReadFromJsonAsync<TodoItem>();

        if (todo == null || string.IsNullOrWhiteSpace(todo.Title))
        {
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Title is required");
            return badRequestResponse;
        }

        var created = await _todoService.CreateAsync(todo);

        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/todos/{created.Id}");
        await response.WriteAsJsonAsync(created, HttpStatusCode.Created);
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
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Title is required");
            return badRequestResponse;
        }

        var existing = await _todoService.UpdateAsync(id, updatedTodo);

        if (existing == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            return notFoundResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(existing, HttpStatusCode.OK);
        return response;
    }

    [Function("DeleteTodo")]
    public async Task<HttpResponseData> DeleteTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var deleted = await _todoService.DeleteAsync(id);

        if (!deleted)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            return notFoundResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.NoContent);
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
