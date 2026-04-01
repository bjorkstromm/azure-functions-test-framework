using AzureFunctions.TestFramework.Http.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Sample.FunctionApp.CustomRoutePrefix.AspNetCore;
using Xunit;

namespace Sample.FunctionApp.CustomRoutePrefix.WAF.Tests;

/// <summary>
/// Fixture for <see cref="FunctionsWebApplicationFactory{TProgram}"/> that bridges xUnit's
/// synchronous fixture teardown to the factory's async disposal path.
/// </summary>
public sealed class ProductFunctionsFixture : IAsyncLifetime, IDisposable
{
    public FunctionsWebApplicationFactory<Program> Factory { get; private set; } = default!;

    public Task InitializeAsync()
    {
        Factory = new FunctionsWebApplicationFactory<Program>();
        return Task.CompletedTask;
    }

    public Task ResetAsync()
    {
        var productService = Factory.Services.GetRequiredService<IProductService>();
        var inMemoryProductService = Assert.IsType<InMemoryProductService>(productService);
        inMemoryProductService.Reset();
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
