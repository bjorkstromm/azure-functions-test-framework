using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using Xunit;

namespace Sample.FunctionApp.Worker.Tests;

/// <summary>
/// Shared host fixture for suites that want to amortize worker startup across tests.
/// Tests using this fixture should reset mutable application state during setup.
/// </summary>
public sealed class SharedFunctionsTestHostFixture : IAsyncLifetime
{
    public IFunctionsTestHost TestHost { get; private set; } = default!;

    public HttpClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        TestHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .BuildAndStartAsync();

        Client = TestHost.CreateHttpClient();
    }

    public Task ResetAsync()
    {
        var todoService = TestHost.Services.GetRequiredService<ITodoService>();
        var inMemoryTodoService = Assert.IsType<InMemoryTodoService>(todoService);
        inMemoryTodoService.Reset();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await TestHost.DisposeAsync();
    }
}
