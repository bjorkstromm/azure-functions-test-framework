namespace TestProject;

/// <summary>
/// Custom route-prefix test implementation for the IHostBuilder gRPC variant.
/// </summary>
public class CustomRoutePrefixTests : CustomRoutePrefixTestsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomRoutePrefixTests"/> class.
    /// </summary>
    /// <param name="output">Test output sink.</param>
    public CustomRoutePrefixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Creates and starts the test host.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CustomRoutePrefix.HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
