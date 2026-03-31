using AzureFunctions.TestFramework.Http.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace Sample.FunctionApp.Worker.WAF.NUnit.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample using <see cref="FunctionsWebApplicationFactory{TProgram}"/>
/// with NUnit.
/// <para>
/// A single factory is started once for the entire fixture (<see cref="OneTimeSetUpAttribute"/>) and each
/// test resets the in-memory service state in <see cref="SetUpAttribute"/> to maintain isolation.
/// </para>
/// </summary>
[TestFixture]
public class FunctionsWebApplicationFactoryTests
{
    private FunctionsWebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new FunctionsWebApplicationFactory<Program>();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_factory != null)
            await _factory.DisposeAsync();
    }

    [SetUp]
    public void SetUp()
    {
        // Reset per-test state.
        var todoService = _factory!.Services.GetRequiredService<ITodoService>();
        var inMemory = (InMemoryTodoService)todoService;
        inMemory.Reset();

        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
    }

    [Test]
    public async Task GetTodos_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client!.GetAsync("/api/todos");
        TestContext.Progress.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CreateAndGetTodo_WorksEndToEnd()
    {
        // Act
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "NUnit WAF Test" });

        // Assert
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(created.Title, Is.EqualTo("NUnit WAF Test"));

        // Act
        var getResponse = await _client!.GetAsync($"/api/todos/{created.Id}");

        // Assert
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DeleteTodo_ReturnsNoContent_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "To Delete" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        // Act
        var deleteResponse = await _client!.DeleteAsync($"/api/todos/{created!.Id}");

        // Assert
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task WithWebHostBuilder_CanOverrideServices()
    {
        // Arrange
        var seededTodo = new TodoItem
        {
            Id = "nunit-seeded-id",
            Title = "NUnit WAF Seeded Todo",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        using var customFactory = _factory!.WithWebHostBuilder(builder =>
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Has.Count.EqualTo(1));
        Assert.That(todos![0].Title, Is.EqualTo("NUnit WAF Seeded Todo"));
    }

    [Test]
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "nunit-correlation-id");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CorrelationId, Is.EqualTo("nunit-correlation-id"));
    }

    [Test]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        // Act
        var response = await _client!.GetAsync("/api/correlation");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CorrelationId, Is.Null);
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
