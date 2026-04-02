using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Integration tests for the Worker SDK 2.x sample app using <see cref="FunctionsTestHost"/>
/// in direct gRPC mode with <c>IHostApplicationBuilder</c>
/// (<c>FunctionsApplication.CreateBuilder</c> + <c>ConfigureFunctionsWorkerDefaults</c>).
/// Uses <c>Program.CreateWorkerHostApplicationBuilder</c> so the worker is bootstrapped via
/// the modern minimal-hosting API.
/// Inherits common tests from <see cref="TodoFunctionsCoreTestsBase"/>.
/// </summary>
public class TodoFunctionsHostAppBuilderTests : TodoFunctionsCoreTestsBase
{
    public TodoFunctionsHostAppBuilderTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(CreateLoggerFactory(Output))
            .WithHostApplicationBuilderFactory(Program.CreateWorkerHostApplicationBuilder)
            .BuildAndStartAsync();

    // ── Mode-specific tests ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateTodo_ReturnsTodo_WithGeneratedId()
    {
        var response = await Client!.PostAsJsonAsync("/api/todos", new { Title = "HostAppBuilder Task" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.NotNull(todo);
        Assert.False(string.IsNullOrEmpty(todo!.Id.ToString()));
        Assert.Equal("HostAppBuilder Task", todo.Title);
        Assert.False(todo.IsCompleted);
    }

    [Fact]
    public async Task UpdateTodo_UpdatesExistingTodo()
    {
        var createResponse = await Client!.PostAsJsonAsync("/api/todos", new { Title = "Original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoDto>();

        var response = await Client!.PutAsJsonAsync(
            $"/api/todos/{created!.Id}",
            new { Title = "Updated", IsCompleted = true });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<TodoDto>();
        Assert.Equal("Updated", updated!.Title);
        Assert.True(updated.IsCompleted);
    }

    [Fact]
    public async Task InvokeTimerAsync_WithDefaultTimerInfo_Succeeds()
    {
        var result = await TestHost!.InvokeTimerAsync("HeartbeatTimer");
        Output.WriteLine($"Success: {result.Success}, Error: {result.Error}");

        Assert.True(result.Success, $"Timer invocation failed: {result.Error}");
    }
}
