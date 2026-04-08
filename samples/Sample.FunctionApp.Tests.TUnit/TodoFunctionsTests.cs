using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp;
using System.Net;
using System.Net.Http.Json;
using TUnit.Core;

namespace Sample.FunctionApp.Tests.TUnit;

/// <summary>
/// Sample TUnit integration tests for the Todo API.
/// Demonstrates using FunctionsTestHost with TUnit per-test lifecycle via [Before(Test)]/[After(Test)].
/// </summary>
public class TodoFunctionsTests
{
    private IFunctionsTestHost? _host;
    private HttpClient? _client;

    [Before(Test)]
    public async Task SetUp()
    {
        var loggerProvider = new TUnitLoggerProvider();
        _host = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(loggerProvider)))
            .ConfigureWorkerLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddProvider(loggerProvider);
            })
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
            .BuildAndStartAsync();
        _client = _host.CreateHttpClient();
    }

    [After(Test)]
    public async Task TearDown()
    {
        _client?.Dispose();
        if (_host != null) await _host.DisposeAsync();
    }

    [Test]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        var response = await _client!.GetAsync("/api/todos");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        await Assert.That(todos).IsNotNull();
        await Assert.That(todos!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Buy milk" });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        await Assert.That(todo).IsNotNull();
        await Assert.That(todo!.Id).IsNotEmpty();
    }

    [Test]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client!.GetAsync("/api/health");
        await Assert.That(response.IsSuccessStatusCode).IsTrue();
    }
}
