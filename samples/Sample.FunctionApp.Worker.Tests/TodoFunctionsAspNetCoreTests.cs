using AzureFunctions.TestFramework.Core;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Sample integration tests using <see cref="FunctionsTestHost"/> in ASP.NET Core / Kestrel mode.
/// </summary>
public class TodoFunctionsAspNetCoreTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private IFunctionsTestHost _testHost = default!;
    private HttpClient _client = default!;

    public async ValueTask InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);

        _client = _testHost.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _testHost.StopAsync(TestCancellation);
        _testHost.Dispose();
    }

    [Fact]
    public async Task CreateTodo_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { Title = "ASP.NET Core Task" }, TestCancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.NotNull(todo);
        Assert.Equal("ASP.NET Core Task", todo!.Title);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health", TestCancellation);

        response.EnsureSuccessStatusCode();
    }
}

