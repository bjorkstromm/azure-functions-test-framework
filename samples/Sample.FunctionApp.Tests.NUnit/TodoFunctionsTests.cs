using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Sample.FunctionApp;
using System.Net;
using System.Net.Http.Json;

namespace Sample.FunctionApp.Tests.NUnit;

/// <summary>
/// Sample NUnit integration tests for the Todo API.
/// Demonstrates using FunctionsTestHost with NUnit per-test isolation via SetUp/TearDown.
/// </summary>
[TestFixture]
public class TodoFunctionsTests
{
    private IFunctionsTestHost? _host;
    private HttpClient? _client;

    [SetUp]
    public async Task SetUp()
    {
        _host = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new NUnitLoggerProvider())))
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
            .BuildAndStartAsync();
        _client = _host.CreateHttpClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client?.Dispose();
        if (_host != null) await _host.DisposeAsync();
    }

    [Test]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        var response = await _client!.GetAsync("/api/todos");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.That(todos, Is.Not.Null);
        Assert.That(todos!, Is.Empty);
    }

    [Test]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Buy milk" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Id, Is.Not.Empty);
        Assert.That(todo.Title, Is.EqualTo("Buy milk"));
    }

    [Test]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client!.GetAsync("/api/health");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }
}
