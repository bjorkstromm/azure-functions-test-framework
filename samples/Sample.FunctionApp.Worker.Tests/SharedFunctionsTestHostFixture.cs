using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Shared host fixture for suites that want to amortize worker startup across tests.
/// Tests using this fixture should reset mutable application state during setup.
/// </summary>
public sealed class SharedFunctionsTestHostFixture : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public IFunctionsTestHost TestHost { get; private set; } = default!;

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public HttpClient Client { get; private set; } = default!;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        TestHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);

        Client = TestHost.CreateHttpClient();
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public Task ResetAsync()
    {
        var todoService = TestHost.Services.GetRequiredService<ITodoService>();
        var inMemoryTodoService = Assert.IsType<InMemoryTodoService>(todoService);
        inMemoryTodoService.Reset();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await TestHost.DisposeAsync();
    }
}
