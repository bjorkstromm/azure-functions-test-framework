using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace TestProject;

/// <summary>Base for tests that use a shared host fixture to amortize startup time.</summary>
public sealed class SharedFunctionsTestHostFixture : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    public IFunctionsTestHost TestHost { get; private set; } = default!;
    public HttpClient Client { get; private set; } = default!;

    public Func<Task<IFunctionsTestHost>> HostFactory { get; set; } = default!;

    public async ValueTask InitializeAsync()
    {
        TestHost = await HostFactory();
        Client = TestHost.CreateHttpClient();
    }

    public Task ResetAsync()
    {
        var itemService = TestHost.Services.GetRequiredService<IItemService>();
        (itemService as InMemoryItemService)?.Reset();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await TestHost.DisposeAsync();
    }
}
