using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace Sample.FunctionApp.Worker.NUnit.Tests;

/// <summary>
/// Integration tests demonstrating the shared-host pattern with NUnit.
/// A single <see cref="FunctionsTestHost"/> is started once for all tests in this fixture
/// (<see cref="OneTimeSetUpAttribute"/>/<see cref="OneTimeTearDownAttribute"/>), and each
/// test resets the in-memory service state in <see cref="SetUp"/> to maintain isolation.
/// This pattern amortises worker startup time across the entire fixture.
/// </summary>
[TestFixture]
public class SharedHostTests
{
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .BuildAndStartAsync();

        _client = _testHost.CreateHttpClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [SetUp]
    public void SetUp()
    {
        // Reset mutable state so each test starts with a clean slate.
        var todoService = _testHost!.Services.GetRequiredService<ITodoService>();
        var inMemory = (InMemoryTodoService)todoService;
        inMemory.Reset();
    }

    [Test]
    public async Task SharedHost_GetTodos_ReturnsEmptyList_AfterReset()
    {
        // Act
        var todos = await _client!.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        // Assert
        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }

    [Test]
    public async Task SharedHost_CanCreateTodo()
    {
        // Act
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Shared host item" });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(todo.Title, Is.EqualTo("Shared host item"));
    }

    [Test]
    public async Task SharedHost_ResetKeepsTestsIsolated()
    {
        // Arrange – create an item in a previous test would not be visible here thanks to Reset()
        var todos = await _client!.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        // Assert
        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }

    [Test]
    public async Task SharedHost_Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/api/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
