using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Shared base class for the four-mode test matrix:
/// <list type="bullet">
///   <item><see cref="TodoFunctionsTests"/> — <see cref="IHostBuilder"/> + direct gRPC mode</item>
///   <item><see cref="TodoFunctionsAspNetCoreTests"/> — <see cref="IHostBuilder"/> + ASP.NET Core / Kestrel mode</item>
///   <item><see cref="TodoFunctionsHostAppBuilderTests"/> — <see cref="Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder"/> + direct gRPC mode</item>
///   <item><see cref="TodoFunctionsHostAppBuilderAspNetCoreTests"/> — <see cref="Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder"/> + ASP.NET Core / Kestrel mode</item>
/// </list>
/// Each concrete subclass overrides <see cref="CreateTestHostAsync"/> to supply the appropriate
/// <see cref="IFunctionsTestHost"/>.  All common test methods are defined here and are
/// inherited by every subclass, providing a complete four-mode test matrix with minimal
/// duplication.
/// </summary>
public abstract class TodoFunctionsCoreTestsBase : IAsyncLifetime
{
    protected readonly ITestOutputHelper Output;
    protected IFunctionsTestHost? TestHost;
    protected HttpClient? Client;

    protected TodoFunctionsCoreTestsBase(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>Creates and starts a <see cref="IFunctionsTestHost"/> for the mode under test.</summary>
    protected abstract Task<IFunctionsTestHost> CreateTestHostAsync();

    public async Task InitializeAsync()
    {
        TestHost = await CreateTestHostAsync();
        Client = TestHost.CreateHttpClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (TestHost != null)
        {
            await TestHost.StopAsync();
            TestHost.Dispose();
        }
    }

    // ── Common tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        var response = await Client!.GetAsync("/api/todos");
        Output.WriteLine($"Status: {response.StatusCode}");

        response.EnsureSuccessStatusCode();
        var todos = await response.Content.ReadFromJsonAsync<List<TodoDto>>();
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }

    [Fact]
    public async Task GetTodo_ReturnsTodo_WhenExists()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Find Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.GetAsync($"/api/todos/{created!.Id}");

        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.Equal(created.Id, todo!.Id);
    }

    [Fact]
    public async Task GetTodo_ReturnsNotFound_WhenDoesNotExist()
    {
        var response = await Client!.GetAsync($"/api/todos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTodo_RemovesTodo_WhenExists()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Delete Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.DeleteAsync($"/api/todos/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"/api/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Client!.GetAsync("/api/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetTodoByBindingData_ReturnsTodo_WhenRouteParamInBindingData()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Binding Data Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.GetAsync($"/api/todos/{created!.Id}/binding-data");

        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("Binding Data Test", todo.Title);
    }

    [Fact]
    public async Task GetTodoWithContext_ReturnsTodo_WhenFunctionContextInjectedAsParameter()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Context Test" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.GetAsync($"/api/todos/{created!.Id}/with-context");

        response.EnsureSuccessStatusCode();
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.Equal(created.Id, todo!.Id);
        Assert.Equal("Context Test", todo.Title);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected static ILoggerFactory CreateLoggerFactory(ITestOutputHelper output) =>
        LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(output)));
}
