using System.Net;
using System.Net.Http.Json;
using Sample.FunctionApp.Worker;
using Xunit;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Demonstrates the optional shared-host pattern for faster gRPC test suites.
/// </summary>
public sealed class FunctionsTestHostReuseFixtureTests :
    IClassFixture<SharedFunctionsTestHostFixture>,
    IAsyncLifetime
{
    private readonly SharedFunctionsTestHostFixture _fixture;

    public FunctionsTestHostReuseFixtureTests(SharedFunctionsTestHostFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SharedFixture_CanCreateTodo()
    {
        // Act
        var response = await _fixture.Client.PostAsJsonAsync("/api/todos", new { Title = "Shared host item" });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.Equal("Shared host item", todo.Title);
    }

    [Fact]
    public async Task SharedFixture_ResetKeepsTestsIsolated()
    {
        // Act
        var todos = await _fixture.Client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        // Assert
        Assert.NotNull(todos);
        Assert.Empty(todos);
    }
}
