using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sample.FunctionApp;

namespace Sample.FunctionApp.WebApplicationFactory.Tests;

/// <summary>
/// Integration tests that use <see cref="FunctionsWebApplicationFactory{TProgram}"/> — a
/// <c>WebApplicationFactory</c>-based approach that runs the Azure Functions app through its
/// full ASP.NET Core pipeline (including middleware and services from <c>Program.cs</c>).
/// </summary>
public class FunctionsWebApplicationFactoryTests : IClassFixture<FunctionsWebApplicationFactory<Program>>
{
    private readonly FunctionsWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public FunctionsWebApplicationFactoryTests(
        FunctionsWebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTodos_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/todos");

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        _output.WriteLine($"Status Code: {response.StatusCode}");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetTodo_WorksEndToEnd()
    {
        // Arrange
        var newTodo = new { Title = "WebApplicationFactory Test" };

        // Act – create
        var createResponse = await _client.PostAsJsonAsync("/api/todos", newTodo);

        _output.WriteLine($"Create Status: {createResponse.StatusCode}");

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(created);
        Assert.Equal("WebApplicationFactory Test", created.Title);

        // Act – retrieve
        var getResponse = await _client.GetAsync($"/api/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task WithWebHostBuilder_CanOverrideServices()
    {
        // Demonstrates the WebApplicationFactory pattern: swap ITodoService for a pre-seeded stub.
        // The stub is registered last (after Program.cs), so the DI container resolves it
        // instead of the default InMemoryTodoService — proving that service overrides work.
        var seededTodo = new TodoItem
        {
            Id = "waf-seeded-id",
            Title = "WAF Seeded Todo",
            IsCompleted = false,
            CreatedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the default InMemoryTodoService and replace with a pre-seeded stub.
                services.RemoveAll<ITodoService>();
                services.AddSingleton<ITodoService>(new SeededTodoService(seededTodo));
            });
        });

        using var customClient = customFactory.CreateClient();

        // Act
        var response = await customClient.GetAsync("/api/todos");

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        // Assert — the response must contain the seeded item, not an empty list.
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.NotNull(todos);
        Assert.Single(todos);
        Assert.Equal("waf-seeded-id", todos[0].Id);
        Assert.Equal("WAF Seeded Todo", todos[0].Title);
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
