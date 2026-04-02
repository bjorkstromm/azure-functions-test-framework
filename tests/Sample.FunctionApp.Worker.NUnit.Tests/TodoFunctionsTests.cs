using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Net;

using System.Net.Http.Json;

namespace Sample.FunctionApp.Worker.NUnit.Tests;

/// <summary>
/// Integration tests demonstrating the host-per-test pattern with NUnit.
/// Each test creates its own <see cref="FunctionsTestHost"/> and tears it down afterwards,
/// providing full isolation at the cost of a slightly longer startup time per test.
/// </summary>
[TestFixture]
public class TodoFunctionsTests
{
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    [SetUp]
    public async Task SetUp()
    {
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new NUnitLoggerProvider())))
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>());

        _testHost = await builder.BuildAndStartAsync();
        _client = _testHost.CreateHttpClient();
    }

    [TearDown]
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
        TestContext.Progress.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }

    [Test]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        // Act
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "NUnit Task" });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(todo.Title, Is.EqualTo("NUnit Task"));
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
    public async Task GetTodo_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync($"/api/todos/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        // Act
        var response = await _client!.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new { Title = "Updated", IsCompleted = true });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(updated!.Title, Is.EqualTo("Updated"));
        Assert.That(updated.IsCompleted, Is.True);
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

    [TestCase("GET", "probe")]
    [TestCase("HEAD", "")]
    [TestCase("OPTIONS", "")]
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader(string method, string expectedBody)
    {
        // Arrange
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/http-verbs-probe");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo(expectedBody));
        Assert.That(response.Headers.TryGetValues("X-Probe-Method", out var values), Is.True);
        Assert.That(values!.Single(), Is.EqualTo(method).IgnoreCase);
    }

    [Test]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        // Act
        var result = await _testHost!.InvokeTimerAsync("HeartbeatTimer");
        TestContext.Progress.WriteLine($"Success: {result.Success}, Error: {result.Error}");

        // Assert
        Assert.That(result.Success, Is.True, $"Timer invocation failed: {result.Error}");
    }
}
