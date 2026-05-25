using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace Sample.FunctionApp;

public class TodoFunctions
{
    private readonly ITodoService _todoService;

    public TodoFunctions(ITodoService todoService) => _todoService = todoService;

    [Function("GetTodos")]
    public async Task<HttpResponseData> GetTodos(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(_todoService.GetAll());
        return response;
    }

    [Function("GetTodo")]
    public async Task<HttpResponseData> GetTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var todo = _todoService.GetById(id);
        if (todo == null) return req.CreateResponse(HttpStatusCode.NotFound);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(todo);
        return response;
    }

    [Function("CreateTodo")]
    public async Task<HttpResponseData> CreateTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<CreateTodoRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Title))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Title is required");
            return bad;
        }

        var todo = _todoService.Create(body.Title);
        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/todos/{todo.Id}");
        await response.WriteAsJsonAsync(todo);
        return response;
    }

    [Function("DeleteTodo")]
    public HttpResponseData DeleteTodo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "todos/{id}")] HttpRequestData req,
        string id)
    {
        var deleted = _todoService.Delete(id);
        return req.CreateResponse(deleted ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
    }

    [Function("Health")]
    public async Task<HttpResponseData> Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { status = "Healthy" });
        return response;
    }
}

public sealed record CreateTodoRequest(string Title);
