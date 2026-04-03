using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample using <see cref="FunctionsTestHost"/> in
/// ASP.NET Core / Kestrel mode (<c>ConfigureFunctionsWebApplication</c>).
/// Uses <c>Program.CreateHostBuilder</c> so the worker starts a real Kestrel server and requests
/// are routed through the full ASP.NET Core middleware pipeline.
/// </summary>
public class TodoFunctionsAspNetCoreTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public TodoFunctionsAspNetCoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);

        _client = _testHost.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync(TestCancellation);
            _testHost.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        // Act
        var response = await _client!.GetAsync("/api/todos", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoDto>>(TestCancellation);
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        // Act
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "ASP.NET Core Task" }, TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.NotNull(todo);
        Assert.False(string.IsNullOrEmpty(todo!.Id.ToString()));
        Assert.Equal("ASP.NET Core Task", todo.Title);
        Assert.False(todo.IsCompleted);
    }

    [Fact]
    public async Task GetTodo_ReturnsTodo_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Find Me" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}", TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
    }

    [Fact]
    public async Task GetTodo_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync($"/api/todos/{Guid.NewGuid()}", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Original" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act
        var response = await _client!.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new { Title = "Updated", IsCompleted = true },
            TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal("Updated", updated!.Title);
        Assert.True(updated.IsCompleted);
    }

    [Fact]
    public async Task DeleteTodo_RemovesTodo_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Delete Me" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act
        var response = await _client!.DeleteAsync($"/api/todos/{created!.Id}", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/todos/{created.Id}", TestCancellation);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/api/health", TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData("GET", "probe", false)]
    [InlineData("HEAD", "", false)]
    [InlineData("OPTIONS", "", false)]
    [InlineData("PATCH", "PATCH", true)]
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader_InKestrelMode(string method, string expectedBody, bool requestBody)
    {
        // Arrange
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/http-verbs-probe");
        if (requestBody)
        {
            request.Content = new StringContent(method);
        }

        // Act
        var response = await _client!.SendAsync(request, TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal(expectedBody, body);
        Assert.True(response.Headers.TryGetValues("X-Probe-Method", out var values));
        Assert.Equal(method, Assert.Single(values), ignoreCase: true);
    }

    [Fact]
    public async Task GetTodoByBindingData_ReturnsTodo_WhenRouteParamInBindingData()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "ASP.NET Core Binding Data Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act — endpoint reads id exclusively from BindingContext.BindingData, not from a direct parameter
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/binding-data", TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("ASP.NET Core Binding Data Test", todo.Title);
    }

    [Fact]
    public async Task GetTodoWithContext_ReturnsTodo_WhenFunctionContextInjectedAsParameter()
    {
        // Arrange — GetTodoWithContext takes FunctionContext as a direct function parameter
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "ASP.NET Core Context Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/with-context", TestCancellation);

        // Assert — FunctionContext must be non-null; function returns 500 otherwise
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("ASP.NET Core Context Test", todo.Title);
    }

    [Fact]
    public async Task ConfigureServices_CanOverrideServicesInKestrelMode()
    {
        // Arrange
        var seededTodo = new TodoItem
        {
            Id = "aspnetcore-seeded-id",
            Title = "Seeded via ConfigureServices in Kestrel mode",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var seededService = new SeededTodoService(seededTodo);

        await using var overrideHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .ConfigureServices(services =>
            {
                services.RemoveAll<ITodoService>();
                services.AddSingleton<ITodoService>(seededService);
            })
            .BuildAndStartAsync(TestCancellation);

        using var customClient = overrideHost.CreateHttpClient();

        // Act
        var response = await customClient.GetAsync("/api/todos", TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>(TestCancellation);
        Assert.NotNull(todos);
        Assert.Single(todos);
        Assert.Equal("Seeded via ConfigureServices in Kestrel mode", todos![0].Title);
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
