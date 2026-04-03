namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Demonstrates the optional shared-host pattern for faster gRPC test suites.
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
        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/api/todos", new { Title = "Shared host item" }, TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var todo = await response.Content.ReadFromJsonAsync<TodoDto>(TestCancellation);
        Assert.NotNull(todo);
        await Verify(todo);
    }

    [Fact]
    public async Task SharedFixture_ResetKeepsTestsIsolated()
    {
        // Act
        var todos = await _fixture.Client.GetFromJsonAsync<List<TodoItem>>("/api/todos", TestCancellation);

        // Assert
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }
}
