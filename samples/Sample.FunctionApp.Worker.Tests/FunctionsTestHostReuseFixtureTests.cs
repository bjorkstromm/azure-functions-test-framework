namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Demonstrates the optional shared-host pattern for faster test suites.
/// </summary>
public sealed class FunctionsTestHostReuseFixtureTests :
    IClassFixture<SharedFunctionsTestHostFixture>,
    IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly SharedFunctionsTestHostFixture _fixture;

    public FunctionsTestHostReuseFixtureTests(SharedFunctionsTestHostFixture fixture)
    {
        _fixture = fixture;
    }

    public ValueTask InitializeAsync() => new(_fixture.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SharedFixture_CanCreateTodo()
    {
        var response = await _fixture.Client.PostAsJsonAsync("/api/todos", new { Title = "Shared host item" }, TestCancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SharedFixture_ResetKeepsTestsIsolated()
    {
        var todos = await _fixture.Client.GetFromJsonAsync<List<TodoItem>>("/api/todos", TestCancellation);

        Assert.NotNull(todos);
        Assert.Empty(todos);
    }
}
