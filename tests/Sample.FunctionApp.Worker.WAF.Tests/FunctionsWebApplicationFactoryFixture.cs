using AzureFunctions.TestFramework.Http.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.Worker;
using Xunit;

namespace Sample.FunctionApp.Worker.WAF.Tests;

/// <summary>
/// Fixture for <see cref="FunctionsWebApplicationFactory{TProgram}"/> that bridges xUnit's
/// synchronous fixture teardown to the factory's async disposal path.
/// </summary>
public sealed class FunctionsWebApplicationFactoryFixture : IAsyncLifetime, IDisposable
{
    public FunctionsWebApplicationFactory<Program> Factory { get; private set; } = default!;

    public Task InitializeAsync()
    {
        Factory = new FunctionsWebApplicationFactory<Program>();
        return Task.CompletedTask;
    }

    public Task ResetAsync()
    {
        var todoService = Factory.Services.GetRequiredService<ITodoService>();
        var inMemoryTodoService = Assert.IsType<InMemoryTodoService>(todoService);
        inMemoryTodoService.Reset();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
    }

    public void Dispose()
    {
        Factory.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
