using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;

namespace Sample.FunctionApp.Worker.NUnit.Tests;

/// <summary>
/// Shared base class for the four-mode NUnit test matrix:
/// <list type="bullet">
///   <item><see cref="TodoFunctionsTests"/> — <see cref="IHostBuilder"/> + direct gRPC mode</item>
///   <item><see cref="TodoFunctionsAspNetCoreTests"/> — <see cref="IHostBuilder"/> + ASP.NET Core / Kestrel mode</item>
///   <item><see cref="TodoFunctionsHostAppBuilderTests"/> — <see cref="Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder"/> + direct gRPC mode</item>
///   <item><see cref="TodoFunctionsHostAppBuilderAspNetCoreTests"/> — <see cref="Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder"/> + ASP.NET Core / Kestrel mode</item>
/// </list>
/// Each concrete subclass overrides <see cref="CreateTestHostAsync"/> to supply the appropriate
/// <see cref="IFunctionsTestHost"/>.  Common test methods are defined here and inherited by
/// every subclass.
/// </summary>
public abstract class TodoFunctionsCoreTestsBase
{
    protected IFunctionsTestHost? TestHost;
    protected HttpClient? Client;

    /// <summary>Creates and starts a <see cref="IFunctionsTestHost"/> for the mode under test.</summary>
    protected abstract Task<IFunctionsTestHost> CreateTestHostAsync();

    [SetUp]
    public async Task SetUpBase()
    {
        TestHost = await CreateTestHostAsync();
        Client = TestHost.CreateHttpClient();
    }

    [TearDown]
    public async Task TearDownBase()
    {
        Client?.Dispose();
        if (TestHost != null)
        {
            await TestHost.StopAsync();
            TestHost.Dispose();
        }
    }

    // ── Common tests ──────────────────────────────────────────────────────────

    [Test]
    public async Task GetTodos_ReturnsEmptyList_WhenNoTodosExist()
    {
        var response = await Client!.GetAsync("/api/todos");
        TestContext.Progress.WriteLine($"Status: {response.StatusCode}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var todos = await response.Content.ReadFromJsonAsync<List<TodoItem>>();
        Assert.That(todos, Is.Not.Null);
        Assert.That(todos, Is.Empty);
    }

    [Test]
    public async Task GetTodo_ReturnsTodo_WhenExists()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Find Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        var response = await Client!.GetAsync($"/api/todos/{created!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var todo = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.That(todo!.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public async Task GetTodo_ReturnsNotFound_WhenDoesNotExist()
    {
        var response = await Client!.GetAsync($"/api/todos/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteTodo_RemovesTodo_WhenExists()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Delete Me" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoItem>();

        var response = await Client!.DeleteAsync($"/api/todos/{created!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/todos/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Health_ReturnsOk()
    {
        var response = await Client!.GetAsync("/api/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(b => b.AddProvider(new NUnitLoggerProvider()));
}
