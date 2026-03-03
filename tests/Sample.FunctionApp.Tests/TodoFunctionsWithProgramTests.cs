using AzureFunctions.TestFramework.Core;
using Sample.FunctionApp;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

/// <summary>
/// Demonstrates that <see cref="FunctionsTestHostBuilder.WithHostBuilderFactory"/> lets tests
/// use the exact same service registrations that <c>Program.cs</c> configures at runtime.
/// Because <c>Program.CreateWorkerHostBuilder</c> already registers <see cref="InMemoryTodoService"/>,
/// no manual <c>ConfigureServices</c> call is needed in the test — the service is inherited
/// from the application's own startup code.
/// </summary>
public class TodoFunctionsWithProgramTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public TodoFunctionsWithProgramTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Use Program.CreateWorkerHostBuilder so that all services registered in Program.cs
        // (including InMemoryTodoService) are automatically available — no need to
        // re-register them in the test.
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .BuildAndStartAsync();

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
        var response = await _client!.GetAsync("/api/todos");

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<Todo>>();
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var newTodo = new { Title = "Program Test Task" };

        var response = await _client!.PostAsJsonAsync("/api/todos", newTodo);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.NotNull(todo);
        Assert.NotEqual(Guid.Empty, todo.Id);
        Assert.Equal("Program Test Task", todo.Title);
        Assert.False(todo.IsCompleted);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client!.GetAsync("/api/health");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<object>();
        Assert.NotNull(result);
    }
}
