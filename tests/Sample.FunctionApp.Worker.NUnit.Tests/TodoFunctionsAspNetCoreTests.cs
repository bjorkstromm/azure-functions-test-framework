using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace Sample.FunctionApp.Worker.NUnit.Tests;

/// <summary>
/// Integration tests using <see cref="FunctionsTestHost"/> in ASP.NET Core / Kestrel mode with
/// <c>IHostBuilder</c>.  Uses <c>Program.CreateHostBuilder</c> so the worker starts a real
/// Kestrel server and requests are routed through the full ASP.NET Core middleware pipeline.
/// Inherits common tests from <see cref="TodoFunctionsCoreTestsBase"/>.
/// </summary>
[TestFixture]
public class TodoFunctionsAspNetCoreTests : TodoFunctionsCoreTestsBase
{
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync();

    // ── Mode-specific tests ───────────────────────────────────────────────────

    [Test]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await Client!.PostAsJsonAsync("/api/todos", new { Title = "NUnit ASP.NET Core Task" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(todo.Title, Is.EqualTo("NUnit ASP.NET Core Task"));
        Assert.That(todo.IsCompleted, Is.False);
    }

    [Test]
    public async Task GetTodo_ReturnsTodo_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Find Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(todo!.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public async Task DeleteTodo_RemovesTodo_WhenExists()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Delete Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        // Act
        var response = await _client!.DeleteAsync($"/api/todos/{created!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await _client.GetAsync($"/api/todos/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("GET", "probe", false)]
    [TestCase("HEAD", "", false)]
    [TestCase("OPTIONS", "", false)]
    [TestCase("PATCH", "PATCH", true)]
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader_InKestrelMode(string method, string expectedBody, bool requestBody)
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(expectedBody));
        Assert.That(response.Headers.TryGetValues("X-Probe-Method", out var values), Is.True);
        Assert.That(values!.Single(), Is.EqualTo(method).IgnoreCase);
    }

    [Test]
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "nunit-aspnetcore-correlation-id");

        var response = await Client!.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.CorrelationId, Is.EqualTo("nunit-aspnetcore-correlation-id"));
    }

    [Test]
    public async Task ConfigureServices_CanOverrideServicesInKestrelMode()
    {
        var seededTodo = new TodoItem
        {
            Id = "nunit-aspnetcore-seeded-id",
            Title = "NUnit ASP.NET Core Seeded Todo",
            IsCompleted = false,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await using var overrideHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .ConfigureServices(services =>
            {
                services.RemoveAll<ITodoService>();
                services.AddSingleton<ITodoService>(new SeededTodoService(seededTodo));
            })
            .BuildAndStartAsync();

        using var customClient = overrideHost.CreateHttpClient();

        var response = await customClient.GetAsync("/api/todos");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Has.Count.EqualTo(1));
        Assert.That(todos![0].Title, Is.EqualTo("NUnit ASP.NET Core Seeded Todo"));
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

