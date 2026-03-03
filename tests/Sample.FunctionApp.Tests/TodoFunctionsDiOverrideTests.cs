using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

/// <summary>
/// Demonstrates that <see cref="FunctionsTestHostBuilder.ConfigureServices"/> allows test code
/// to replace any DI-registered service with a test double.  The worker picks up the override
/// because <see cref="WorkerHostService"/> applies every registered
/// <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> configurator inside
/// <c>ConfigureFunctionsWorkerDefaults</c>, before the host is built.
/// </summary>
public class TodoFunctionsDiOverrideTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public TodoFunctionsDiOverrideTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Replace the default InMemoryTodoService with a pre-seeded stub so we can assert
        // that the overridden service — not the real one — is used by the function.
        var seededTodo = new TodoItem
        {
            Id = "seeded-id-1",
            Title = "Seeded Todo",
            IsCompleted = false,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .ConfigureServices(services =>
                services.AddSingleton<ITodoService>(new SeededTodoService(seededTodo)))
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
    public async Task GetTodos_ReturnsSeededTodos_WhenServiceOverridden()
    {
        // Act
        var response = await _client!.GetAsync("/api/todos");

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.NotNull(todos);
        // The stub returns the seeded item — proves the overridden service is used,
        // not the default InMemoryTodoService (which would return an empty list).
        Assert.Single(todos);
        Assert.Equal("seeded-id-1", todos[0].Id);
        Assert.Equal("Seeded Todo", todos[0].Title);
    }

    [Fact]
    public async Task GetTodo_ReturnsSeededTodo_WhenServiceOverridden()
    {
        // Act
        var response = await _client!.GetAsync("/api/todos/seeded-id-1");

        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(todo);
        Assert.Equal("seeded-id-1", todo.Id);
        Assert.Equal("Seeded Todo", todo.Title);
    }

    [Fact]
    public async Task GetTodo_ReturnsNotFound_ForUnknownId_WhenServiceOverridden()
    {
        // Act – the stub only knows about "seeded-id-1"
        var response = await _client!.GetAsync($"/api/todos/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A minimal <see cref="ITodoService"/> stub pre-seeded with known items, used to verify that
    /// the DI container in the test host resolves the overridden implementation.
    /// </summary>
    private sealed class SeededTodoService : ITodoService
    {
        private readonly List<TodoItem> _todos;

        public SeededTodoService(params TodoItem[] seed)
        {
            _todos = new List<TodoItem>(seed);
        }

        public Task<IEnumerable<TodoItem>> GetAllAsync()
            => Task.FromResult<IEnumerable<TodoItem>>(_todos);

        public Task<TodoItem?> GetByIdAsync(string id)
            => Task.FromResult(_todos.FirstOrDefault(t => t.Id == id));

        public Task<TodoItem> CreateAsync(TodoItem item)
        {
            item.Id = Guid.NewGuid().ToString();
            _todos.Add(item);
            return Task.FromResult(item);
        }

        public Task<TodoItem?> UpdateAsync(string id, TodoItem updates)
        {
            var existing = _todos.FirstOrDefault(t => t.Id == id);
            if (existing == null) return Task.FromResult<TodoItem?>(null);
            existing.Title = updates.Title;
            existing.IsCompleted = updates.IsCompleted;
            return Task.FromResult<TodoItem?>(existing);
        }

        public Task<bool> DeleteAsync(string id)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null) return Task.FromResult(false);
            _todos.Remove(todo);
            return Task.FromResult(true);
        }
    }
}
