using Xunit;

namespace TestProject;

/// <summary>Abstract xUnit lifecycle base — creates an isolated host per test.</summary>
public abstract class TestHostTestBase : IAsyncLifetime
{
    protected static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    protected ITestOutputHelper Output { get; }
    protected IFunctionsTestHost TestHost { get; private set; } = default!;
    protected HttpClient Client { get; private set; } = default!;

    protected TestHostTestBase(ITestOutputHelper output) => Output = output;

    protected abstract Task<IFunctionsTestHost> CreateTestHostAsync();

    protected ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(Output)));

    public async ValueTask InitializeAsync()
    {
        TestHost = await CreateTestHostAsync();
        Client = TestHost.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await TestHost.StopAsync(TestCancellation);
        TestHost.Dispose();
    }
}
