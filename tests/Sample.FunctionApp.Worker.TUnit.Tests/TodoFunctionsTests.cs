using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using System.Net;
using System.Net.Http.Json;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample app using <see cref="FunctionsTestHost"/> and TUnit.
/// </summary>
public class TodoFunctionsTests
{
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    /// <summary>
    /// Builds an isolated function host and HTTP client before each test.
    /// </summary>
    [Before(Test)]
    public async Task SetUp()
    {
        // Arrange
        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new TUnitLoggerProvider())))
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>());

        _testHost = await builder.BuildAndStartAsync();
        _client = _testHost.CreateHttpClient();
    }

    /// <summary>
    /// Tears down the host after each test.
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
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Test Task" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo).IsNotNull();
        await Verify(todo!);
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
        await Verify(updated!);
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

        var getResponse = await _client!.GetAsync($"/api/todos/{created.Id}");
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetTodoByBindingData_ReturnsTodo_WhenRouteParamInBindingData()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Binding Data Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act — endpoint reads id exclusively from BindingContext.BindingData, not from a direct parameter
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/binding-data");

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo!.Id).IsEqualTo(created.Id);
        await Assert.That(todo.Title).IsEqualTo("Binding Data Test");
    }

    [Test]
    public async Task GetTodoAlt_ReturnsTodo_WhenHttpTriggerParamNameIsNotReq()
    {
        // Arrange — GetTodoAlt uses 'request' (not 'req') as the HttpRequestData parameter name
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Alt Binding Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/alt");

        // Assert — verifies framework uses actual binding name from metadata, not hardcoded "req"
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo!.Id).IsEqualTo(created.Id);
        await Assert.That(todo.Title).IsEqualTo("Alt Binding Test");
    }

    [Test]
    public async Task GetTodoWithContext_ReturnsTodo_WhenFunctionContextInjectedAsParameter()
    {
        // Arrange — GetTodoWithContext takes FunctionContext as a direct function parameter
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Context Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/with-context");

        // Assert — FunctionContext must be non-null; function returns 500 otherwise
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo!.Id).IsEqualTo(created.Id);
        await Assert.That(todo.Title).IsEqualTo("Context Test");
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
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader(string method, string expectedBody, bool requestBody)
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
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        // Act
        var result = await _testHost!.InvokeTimerAsync("HeartbeatTimer");
        TestContext.Current?.Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");

        // Assert
        await Assert.That(result.Success).IsTrue();
    }
}

/// <summary>
/// JSON projection for todo API responses used in assertions (matches serialized shape with <see cref="Guid"/> id).
/// </summary>
public class TodoDto
{
    /// <summary>Gets or sets the todo identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the todo title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the todo is completed.</summary>
    public bool IsCompleted { get; set; }
}
