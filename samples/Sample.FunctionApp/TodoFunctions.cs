using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Sample.FunctionApp;

public class TodoFunctions
{
    private static readonly List<TodoItem> Todos = new();

    [Function("GetTodos")]
    public HttpResponseData GetTodos(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteAsJsonAsync(Todos);
        return response;
    }

    [Function("GetTodo")]
    public HttpResponseData GetTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var todo = Todos.FirstOrDefault(t => t.Id == id);
        
        if (todo == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            return notFoundResponse;
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteAsJsonAsync(todo);
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

        todo.Id = Guid.NewGuid().ToString();
        todo.CreatedAt = DateTime.UtcNow;
        Todos.Add(todo);

        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/todos/{todo.Id}");
        await response.WriteAsJsonAsync(todo);
        return response;
    }

    [Function("UpdateTodo")]
    public async Task<HttpResponseData> UpdateTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var existingTodo = Todos.FirstOrDefault(t => t.Id == id);
        
        if (existingTodo == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            return notFoundResponse;
        }

        var updatedTodo = await req.ReadFromJsonAsync<TodoItem>();
        
        if (updatedTodo == null || string.IsNullOrWhiteSpace(updatedTodo.Title))
        {
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Title is required");
            return badRequestResponse;
        }

        existingTodo.Title = updatedTodo.Title;
        existingTodo.IsCompleted = updatedTodo.IsCompleted;
        existingTodo.UpdatedAt = DateTime.UtcNow;

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(existingTodo);
        return response;
    }

    [Function("DeleteTodo")]
    public HttpResponseData DeleteTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var todo = Todos.FirstOrDefault(t => t.Id == id);
        
        if (todo == null)
        {
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            return notFoundResponse;
        }

        Todos.Remove(todo);

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
