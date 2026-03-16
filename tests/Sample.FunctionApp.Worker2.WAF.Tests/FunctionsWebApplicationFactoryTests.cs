using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sample.FunctionApp.Worker2;

namespace Sample.FunctionApp.Worker2.WAF.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample using <see cref="FunctionsWebApplicationFactory{TProgram}"/>.
/// </summary>
public class FunctionsWebApplicationFactoryTests
    : IClassFixture<FunctionsWebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly FunctionsWebApplicationFactory<Program> _factory;
    private HttpClient? _client;
    private readonly ITestOutputHelper _output;

    public FunctionsWebApplicationFactoryTests(
        FunctionsWebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    public Task InitializeAsync()
    {
        if (_factory.Services.GetService(typeof(ITodoService)) is InMemoryTodoService todoService)
            todoService.Reset();

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetTodos_ReturnsSuccessStatusCode()
    {
        var response = await _client!.GetAsync("/api/todos");
        _output.WriteLine($"Status: {response.StatusCode}");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        var response = await _client!.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetTodo_WorksEndToEnd()
    {
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "WAF Worker2 Test" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(created);
        Assert.Equal("WAF Worker2 Test", created.Title);

        var getResponse = await _client!.GetAsync($"/api/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task WithWebHostBuilder_CanOverrideServices()
    {
        var seededTodo = new TodoItem
        {
            Id = "waf2-seeded-id",
            Title = "WAF Worker2 Seeded Todo",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITodoService>();
                services.AddSingleton<ITodoService>(new SeededTodoService(seededTodo));
            });
        });

        using var customClient = customFactory.CreateClient();
        var response = await customClient.GetAsync("/api/todos");
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.NotNull(todos);
        Assert.Single(todos);
        Assert.Equal("waf2-seeded-id", todos[0].Id);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "waf-correlation-id");

        var response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        Assert.Equal("waf-correlation-id", payload.CorrelationId);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        var response = await _client!.GetAsync("/api/correlation");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        Assert.Null(payload.CorrelationId);
    }

    private sealed class SeededTodoService : ITodoService
    {
        private readonly List<TodoItem> _todos;

        public SeededTodoService(params TodoItem[] seed) => _todos = new List<TodoItem>(seed);

        public Task<IEnumerable<TodoItem>> GetAllAsync() => Task.FromResult<IEnumerable<TodoItem>>(_todos);
        public Task<TodoItem?> GetByIdAsync(string id) => Task.FromResult(_todos.FirstOrDefault(t => t.Id == id));

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
