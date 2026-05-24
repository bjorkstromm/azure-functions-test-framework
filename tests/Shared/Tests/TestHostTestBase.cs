using Xunit;

namespace TestProject;

/// <summary>Abstract xUnit lifecycle base — creates an isolated host per test.</summary>
public abstract class TestHostTestBase : IAsyncLifetime
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    protected static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    protected ITestOutputHelper Output { get; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    protected IFunctionsTestHost TestHost { get; private set; } = default!;
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    protected HttpClient Client { get; private set; } = default!;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected TestHostTestBase(ITestOutputHelper output) => Output = output;

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected abstract Task<IFunctionsTestHost> CreateTestHostAsync();

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(Output)));

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        TestHost = await CreateTestHostAsync();
        Client = TestHost.CreateHttpClient();
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await TestHost.StopAsync(TestCancellation);
        TestHost.Dispose();
    }
}
