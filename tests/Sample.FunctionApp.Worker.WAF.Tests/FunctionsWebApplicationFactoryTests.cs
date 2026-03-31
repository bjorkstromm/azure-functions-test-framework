using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sample.FunctionApp.Worker;

namespace Sample.FunctionApp.Worker.WAF.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample using <see cref="FunctionsWebApplicationFactory{TProgram}"/>.
/// </summary>
public class FunctionsWebApplicationFactoryTests
    : IClassFixture<FunctionsWebApplicationFactoryFixture>, IAsyncLifetime
{
    private readonly FunctionsWebApplicationFactoryFixture _fixture;
    private HttpClient? _client;
    private readonly ITestOutputHelper _output;

    public FunctionsWebApplicationFactoryTests(
        FunctionsWebApplicationFactoryFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _client = _fixture.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetTodos_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client!.GetAsync("/api/todos");
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetTodo_WorksEndToEnd()
    {
        // Act
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "WAF Worker Test" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.NotNull(created);
        await Verify(created);

        // Act
        var getResponse = await _client!.GetAsync($"/api/todos/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task WithWebHostBuilder_CanOverrideServices()
    {
        // Arrange
        var seededTodo = new TodoItem
        {
            Id = "waf-seeded-id",
            Title = "WAF Worker Seeded Todo",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        using var customFactory = _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITodoService>();
                services.AddSingleton<ITodoService>(new SeededTodoService(seededTodo));
            });
        });

        using var customClient = customFactory.CreateClient();

        // Act
        var response = await customClient.GetAsync("/api/todos");

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.NotNull(todos);
        await Verify(todos);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "waf-correlation-id");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        await Verify(payload);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        // Act
        var response = await _client!.GetAsync("/api/correlation");

        // Assert
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        await Verify(payload);
    }

    [Fact]
    public async Task GetTodoByBindingData_ReturnsTodo_WhenRouteParamInBindingData()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "WAF Binding Data Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        // Act — endpoint reads id exclusively from BindingContext.BindingData, not from a direct parameter
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/binding-data");

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("WAF Binding Data Test", todo.Title);
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
