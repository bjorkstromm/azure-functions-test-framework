using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp;
using System.Net;
using System.Net.Http.Json;

namespace Sample.FunctionApp.Tests.XUnit;

/// <summary>
/// Sample xUnit integration tests for the Todo API.
/// Demonstrates using FunctionsTestHost with xUnit per-test isolation via IAsyncLifetime.
/// </summary>
public class TodoFunctionsTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _host;
    private HttpClient? _client;

    public TodoFunctionsTests(ITestOutputHelper output) => _output = output;

    public async ValueTask InitializeAsync()
    {
        var loggerProvider = new XUnitLoggerProvider(_output);
        _host = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(loggerProvider)))
            .ConfigureWorkerLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddProvider(loggerProvider);
            })
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
            .BuildAndStartAsync(TestCancellation);
        _client = _host.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_host != null) await _host.DisposeAsync();
    }

    [Fact]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        var response = await _client!.GetAsync("/api/todos", TestCancellation);
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>(TestCancellation);
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Buy milk" }, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>(TestCancellation);
        Assert.NotNull(todo);
        Assert.NotEmpty(todo.Id);
        Assert.Equal("Buy milk", todo.Title);
    }

    [Fact]
    public async Task GetTodo_ReturnsTodo_WhenExists()
    {
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Find Me" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>(TestCancellation);

        var response = await _client!.GetAsync($"/api/todos/{created!.Id}", TestCancellation);
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
    }

    [Fact]
    public async Task DeleteTodo_ReturnsNoContent_WhenExists()
    {
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Delete Me" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>(TestCancellation);

        var response = await _client!.DeleteAsync($"/api/todos/{created!.Id}", TestCancellation);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client!.GetAsync("/api/health", TestCancellation);
        response.EnsureSuccessStatusCode();
    }
}
