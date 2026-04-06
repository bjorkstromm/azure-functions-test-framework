using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Sample integration tests using <see cref="FunctionsTestHost"/> in direct gRPC mode.
/// </summary>
public class TodoFunctionsTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private IFunctionsTestHost _testHost = default!;
    private HttpClient _client = default!;

    public async ValueTask InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
            .BuildAndStartAsync(TestCancellation);

        _client = _testHost.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _testHost.StopAsync(TestCancellation);
        _testHost.Dispose();
    }

    [Fact]
    public async Task GetTodos_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/todos", TestCancellation);

        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoDto>>(TestCancellation);
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task CreateAndGetTodo_RoundTrips()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/todos", new { Title = "Test Task" }, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        var getResponse = await _client.GetAsync($"/api/todos/{created!.Id}", TestCancellation);
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal("Test Task", fetched.Title);
    }

    [Fact]
    public async Task InvokeTimerAsync_Succeeds()
    {
        var result = await _testHost.InvokeTimerAsync("HeartbeatTimer", cancellationToken: TestCancellation);

        Assert.True(result.Success, $"Timer invocation failed: {result.Error}");
    }
}

public class TodoDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

