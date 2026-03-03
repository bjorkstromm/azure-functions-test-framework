using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

public class TodoFunctionsTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public TodoFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Create test host for the sample function app
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>());

        _testHost = await builder.BuildAndStartAsync();
        _client = _testHost.CreateHttpClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Fact]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        // Act
        var response = await _client!.GetAsync("/api/todos");
        
        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<Todo>>();
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        // Arrange
        var newTodo = new { Title = "Test Task" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/todos", newTodo);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.NotNull(todo);
        Assert.NotEqual(Guid.Empty, todo.Id);
        Assert.Equal("Test Task", todo.Title);
        Assert.False(todo.IsCompleted);
    }

    [Fact]
    public async Task GetTodo_ReturnsTodo_WhenExists()
    {
        // Arrange - Create a todo first
        var newTodo = new { Title = "Find Me" };
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", newTodo);
        var createdTodo = await createResponse.Content.ReadFromJsonAsync<Todo>();

        // Act
        var response = await _client.GetAsync($"/api/todos/{createdTodo!.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.NotNull(todo);
        Assert.Equal(createdTodo.Id, todo.Id);
        Assert.Equal("Find Me", todo.Title);
    }

    [Fact]
    public async Task GetTodo_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync($"/api/todos/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        // Arrange - Create a todo first
        var newTodo = new { Title = "Original Title" };
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", newTodo);
        var createdTodo = await createResponse.Content.ReadFromJsonAsync<Todo>();

        // Act
        var updateData = new { Title = "Updated Title", IsCompleted = true };
        var response = await _client.PutAsJsonAsync($"/api/todos/{createdTodo!.Id}", updateData);

        // Assert
        response.EnsureSuccessStatusCode();
        var updatedTodo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.NotNull(updatedTodo);
        Assert.Equal("Updated Title", updatedTodo.Title);
        Assert.True(updatedTodo.IsCompleted);
    }

    [Fact]
    public async Task DeleteTodo_RemovesTodo_WhenExists()
    {
        // Arrange - Create a todo first
        var newTodo = new { Title = "Delete Me" };
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", newTodo);
        var createdTodo = await createResponse.Content.ReadFromJsonAsync<Todo>();

        // Act
        var response = await _client.DeleteAsync($"/api/todos/{createdTodo!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify it's gone
        var getResponse = await _client.GetAsync($"/api/todos/{createdTodo.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<object>();
        Assert.NotNull(result);
    }
}

// Todo model matching the function app
public class Todo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
