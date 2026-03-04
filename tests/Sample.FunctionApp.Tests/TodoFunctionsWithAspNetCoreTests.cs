using AzureFunctions.TestFramework.Core;
using Sample.FunctionApp;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Tests;

/// <summary>
/// Demonstrates that <see cref="FunctionsTestHostBuilder.WithHostBuilderFactory"/> works with
/// <c>Program.CreateHostBuilder</c>, which uses <c>ConfigureFunctionsWebApplication()</c>
/// (ASP.NET Core integration mode).
/// <para>
/// The framework auto-detects that the worker has started an ASP.NET Core HTTP server
/// (by checking for <c>IServer</c> in the worker's DI container) and routes requests via an
/// <see cref="AspNetCoreForwardingHandler"/> instead of the gRPC-direct handler.
/// The worker's <c>GrpcInvocationBridgeStartupFilter</c> fires the required
/// <c>InvocationRequest</c> over gRPC so that <c>WorkerRequestServicesMiddleware</c> can
/// correlate the HTTP request with a <c>FunctionContext</c>.
/// </para>
/// </summary>
public class TodoFunctionsWithAspNetCoreTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public TodoFunctionsWithAspNetCoreTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Use Program.CreateHostBuilder (ConfigureFunctionsWebApplication).
        // The framework will auto-detect ASP.NET Core integration mode and route
        // HTTP requests to the worker's Kestrel server.
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync();

        _client = _testHost.CreateHttpClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Fact]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        var response = await _client!.GetAsync("/api/todos");

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<Todo>>();
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var newTodo = new { Title = "AspNetCore Integration Test Task" };

        var response = await _client!.PostAsJsonAsync("/api/todos", newTodo);

        _output.WriteLine($"Status Code: {response.StatusCode}");
        _output.WriteLine($"Content: {await response.Content.ReadAsStringAsync()}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<Todo>();
        Assert.NotNull(todo);
        Assert.NotEqual(Guid.Empty, todo.Id);
        Assert.Equal("AspNetCore Integration Test Task", todo.Title);
        Assert.False(todo.IsCompleted);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client!.GetAsync("/api/health");

        _output.WriteLine($"Status Code: {response.StatusCode}");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<object>();
        Assert.NotNull(result);
    }
}
