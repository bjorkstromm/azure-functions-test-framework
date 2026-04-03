using System.Net;
using System.Net.Http.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Demonstrates the optional shared-host pattern for faster gRPC test suites (one host per class,
/// in-memory state reset before each test). Includes coverage aligned with the NUnit shared-host sample.
/// </summary>
public sealed class FunctionsTestHostReuseFixtureTests
{
    private static IFunctionsTestHost? s_testHost;
    private static HttpClient? s_client;

    /// <summary>
    /// Starts one shared host for all tests in this class.
    /// </summary>
    [Before(Class)]
    public static async Task ClassSetUp()
    {
        // Arrange
        s_testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .BuildAndStartAsync();

        s_client = s_testHost.CreateHttpClient();
    }

    /// <summary>
    /// Stops the shared host after all tests in this class complete.
    /// </summary>
    [After(Class)]
    public static async Task ClassTearDown()
    {
        s_client?.Dispose();
        if (s_testHost != null)
        {
            await s_testHost.StopAsync();
            s_testHost.Dispose();
        }
    }

    /// <summary>
    /// Clears in-memory todos so tests stay isolated while reusing the same worker process.
    /// </summary>
    [Before(Test)]
    public void ResetMutableApplicationState()
    {
        // Arrange
        var todoService = s_testHost!.Services.GetRequiredService<ITodoService>();
        var inMemory = (InMemoryTodoService)todoService;
        inMemory.Reset();
    }

    [Test]
    public async Task SharedFixture_CanCreateTodo()
    {
        // Act
        var response = await s_client!.PostAsJsonAsync("/api/todos", new { Title = "Shared host item" });

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var todo = await response.Content.ReadFromJsonAsync<TodoDto>();
        await Assert.That(todo).IsNotNull();
        await Verify(todo!);
    }

    [Test]
    public async Task SharedFixture_ResetKeepsTestsIsolated()
    {
        // Act
        var todos = await s_client!.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        // Assert
        await Assert.That(todos).IsNotNull();
        await Assert.That(todos!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SharedHost_Health_ReturnsOk()
    {
        // Act
        var response = await s_client!.GetAsync("/api/health");

        // Assert
        response.EnsureSuccessStatusCode();
    }
}
