using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using System.Net;
using System.Net.Http.Json;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample using <see cref="FunctionsTestHost"/> in
/// ASP.NET Core / Kestrel mode (<c>ConfigureFunctionsWebApplication</c>).
/// Uses <c>Program.CreateHostBuilder</c> so the worker starts a real Kestrel server and requests
/// are routed through the full ASP.NET Core middleware pipeline.
/// </summary>
public class TodoFunctionsAspNetCoreTests
{
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    /// <summary>
    /// Builds a Kestrel-backed function host before each test.
    /// </summary>
    [Before(Test)]
    public async Task SetUp()
    {
        // Arrange
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new TUnitLoggerProvider())))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync();

        _client = _testHost.CreateHttpClient();
    }

    /// <summary>
    /// Disposes the host after each test.
    /// </summary>
    [After(Test)]
    public async Task TearDown()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Test]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        // Act
        var response = await _client!.GetAsync("/api/todos");
        TestContext.Current?.Output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoDto>>();
        await Assert.That(todos).IsNotNull();
        await Assert.That(todos!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        // Act
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "ASP.NET Core Task" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo).IsNotNull();
        await Assert.That(string.IsNullOrEmpty(todo!.Id.ToString())).IsFalse();
        await Assert.That(todo.Title).IsEqualTo("ASP.NET Core Task");
        await Assert.That(todo.IsCompleted).IsFalse();
    }

    [Test]
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
        await Assert.That(todo!.Id).IsEqualTo(created.Id);
    }

    [Test]
    public async Task GetTodo_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync($"/api/todos/{Guid.NewGuid()}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new { Title = "Updated", IsCompleted = true });

        // Assert
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(updated!.Title).IsEqualTo("Updated");
        await Assert.That(updated.IsCompleted).IsTrue();
    }

    [Test]
    public async Task DeleteTodo_RemovesTodo_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Delete Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.DeleteAsync($"/api/todos/{created!.Id}");

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/todos/{created.Id}");
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Test]
    [Arguments("GET", "probe", false)]
    [Arguments("HEAD", "", false)]
    [Arguments("OPTIONS", "", false)]
    [Arguments("PATCH", "PATCH", true)]
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader_InKestrelMode(
        string method,
        string expectedBody,
        bool requestBody)
    {
        // Arrange
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/http-verbs-probe");
        if (requestBody)
        {
            request.Content = new StringContent(method);
        }

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).IsEqualTo(expectedBody);
        await Assert.That(response.Headers.TryGetValues("X-Probe-Method", out var values)).IsTrue();
        var single = values!.Single();
        await Assert.That(single.Equals(method, StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task GetTodoByBindingData_ReturnsTodo_WhenRouteParamInBindingData()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync(
            "/api/todos",
            new { Title = "ASP.NET Core Binding Data Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act — endpoint reads id exclusively from BindingContext.BindingData, not from a direct parameter
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/binding-data");

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo!.Id).IsEqualTo(created.Id);
        await Assert.That(todo.Title).IsEqualTo("ASP.NET Core Binding Data Test");
    }

    [Test]
    public async Task GetTodoWithContext_ReturnsTodo_WhenFunctionContextInjectedAsParameter()
    {
        // Arrange — GetTodoWithContext takes FunctionContext as a direct function parameter
        var createResponse = await _client!.PostAsJsonAsync(
            "/api/todos",
            new { Title = "ASP.NET Core Context Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/with-context");

        // Assert — FunctionContext must be non-null; function returns 500 otherwise
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo!.Id).IsEqualTo(created.Id);
        await Assert.That(todo.Title).IsEqualTo("ASP.NET Core Context Test");
    }

    [Test]
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
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new TUnitLoggerProvider())))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .ConfigureServices(services =>
            {
                services.RemoveAll<ITodoService>();
                services.AddSingleton<ITodoService>(seededService);
            })
            .BuildAndStartAsync();

        using var customClient = overrideHost.CreateHttpClient();

        // Act
        var response = await customClient.GetAsync("/api/todos");

        // Assert
        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        await Assert.That(todos).IsNotNull();
        await Assert.That(todos!.Count).IsEqualTo(1);
        await Assert.That(todos[0].Title).IsEqualTo("Seeded via ConfigureServices in Kestrel mode");
    }

    private sealed class SeededTodoService : ITodoService
    {
        private readonly List<TodoItem> _todos;

        public SeededTodoService(params TodoItem[] seed) => _todos = new List<TodoItem>(seed);

        /// <inheritdoc />
        public Task<IEnumerable<TodoItem>> GetAllAsync() => Task.FromResult<IEnumerable<TodoItem>>(_todos);

        /// <inheritdoc />
        public Task<TodoItem?> GetByIdAsync(string id) => Task.FromResult(_todos.FirstOrDefault(t => t.Id == id));

        /// <inheritdoc />
        public Task<TodoItem> CreateAsync(TodoItem item)
        {
            item.Id = Guid.NewGuid().ToString();
            _todos.Add(item);
            return Task.FromResult(item);
        }

        /// <inheritdoc />
        public Task<TodoItem?> UpdateAsync(string id, TodoItem updates)
        {
            var existing = _todos.FirstOrDefault(t => t.Id == id);
            if (existing == null)
            {
                return Task.FromResult<TodoItem?>(null);
            }

            existing.Title = updates.Title;
            existing.IsCompleted = updates.IsCompleted;
            return Task.FromResult<TodoItem?>(existing);
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(string id)
        {
            var todo = _todos.FirstOrDefault(t => t.Id == id);
            if (todo == null)
            {
                return Task.FromResult(false);
            }

            _todos.Remove(todo);
            return Task.FromResult(true);
        }
    }
}
