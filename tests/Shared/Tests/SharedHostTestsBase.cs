using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TestProject;

/// <summary>Base for tests that use a shared host fixture to amortize startup time.</summary>
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
    /// Gets or sets the value.
    /// </summary>
    public Func<Task<IFunctionsTestHost>> HostFactory { get; set; } = default!;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        TestHost = await HostFactory();
        Client = TestHost.CreateHttpClient();
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public Task ResetAsync()
    {
        var itemService = TestHost.Services.GetRequiredService<IItemService>();
        (itemService as InMemoryItemService)?.Reset();
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
