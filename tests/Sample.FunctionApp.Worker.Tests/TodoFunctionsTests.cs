using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using System.Net.Http.Json;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample app using <see cref="FunctionsTestHost"/>
/// in direct gRPC mode (<c>ConfigureFunctionsWorkerDefaults</c>) with <c>IHostBuilder</c>.
/// Inherits common tests from <see cref="TodoFunctionsCoreTestsBase"/>.
/// </summary>
public class TodoFunctionsTests : TodoFunctionsCoreTestsBase
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public TodoFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask InitializeAsync()
    {
        _testHost = await CreateTestHostAsync();
        _client = _testHost.CreateHttpClient();
    }

    public TodoFunctionsTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory(Output))
            .ConfigureServices(services => services.AddSingleton<ITodoService, InMemoryTodoService>())
            .BuildAndStartAsync();

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
    // ── Mode-specific tests ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        // Act
        var response = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Test Task" }, TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.NotNull(todo);
        await Verify(todo);
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
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.PutAsJsonAsync($"/api/todos/{created!.Id}", new { Title = "Updated", IsCompleted = true });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        await Verify(updated);
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
    public async Task GetTodoByBindingData_ReturnsTodo_WhenRouteParamInBindingData()
    {
        // Arrange
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Binding Data Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act — endpoint reads id exclusively from BindingContext.BindingData, not from a direct parameter
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/binding-data", TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("Binding Data Test", todo.Title);
    }

    [Fact]
    public async Task GetTodoAlt_ReturnsTodo_WhenHttpTriggerParamNameIsNotReq()
    {
        // Arrange — GetTodoAlt uses 'request' (not 'req') as the HttpRequestData parameter name
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Alt Binding Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/alt", TestCancellation);
    public async Task GetTodoAlt_ReturnsTodo_WhenHttpTriggerParamNameIsNotReq()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Alt Binding Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.GetAsync($"/api/todos/{created!.Id}/alt");

        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("Alt Binding Test", todo.Title);
    }

    [Fact]
    public async Task GetTodoWithContext_ReturnsTodo_WhenFunctionContextInjectedAsParameter()
    {
        // Arrange — GetTodoWithContext takes FunctionContext as a direct function parameter
        var createResponse = await _client!.PostAsJsonAsync("/api/todos", new { Title = "Context Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);

        // Act
        var response = await _client!.GetAsync($"/api/todos/{created!.Id}/with-context", TestCancellation);

        // Assert — FunctionContext must be non-null; function returns 500 otherwise
        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("Context Test", todo.Title);
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
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader(string method, string expectedBody, bool requestBody)
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
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        // Act
        var result = await _testHost!.InvokeTimerAsync("HeartbeatTimer", cancellationToken: TestCancellation);
        _output.WriteLine($"Success: {result.Success}, Error: {result.Error}");
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        var result = await TestHost!.InvokeTimerAsync("HeartbeatTimer");
        Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");

        Assert.True(result.Success, $"Timer invocation failed: {result.Error}");
    }
}

public class TodoDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

