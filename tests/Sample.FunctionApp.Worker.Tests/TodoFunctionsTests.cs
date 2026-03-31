using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using System.Net;
using System.Net.Http.Json;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample app using <see cref="FunctionsTestHost"/>.
/// </summary>
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
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
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
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoDto>>();
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        // Act
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Test Task" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        await Verify(todo);
    }

    [Fact]
    public async Task GetTodo_ReturnsTodo_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Find Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.Equal(created.Id, todo!.Id);
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
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.PutAsJsonAsync($"/api/todos/{created!.Id}", new { Title = "Updated", IsCompleted = true });

        // Assert
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Verify(updated);
    }

    [Fact]
    public async Task DeleteTodo_RemovesTodo_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Delete Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.DeleteAsync($"/api/todos/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetTodoByBindingData_ReturnsTodo_WhenRouteParamInBindingData()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Binding Data Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act — endpoint reads id exclusively from BindingContext.BindingData, not from a direct parameter
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/binding-data");

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("Binding Data Test", todo.Title);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        // Act
        var result = await _testHost!.InvokeTimerAsync("HeartbeatTimer");
        _output.WriteLine($"Success: {result.Success}, Error: {result.Error}");

        // Assert
        Assert.True(result.Success, $"Timer invocation failed: {result.Error}");
    }
}

public class TodoDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
