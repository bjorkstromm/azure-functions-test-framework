namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class CustomRoutePrefixTests : CustomRoutePrefixTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public CustomRoutePrefixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CustomRoutePrefix.HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
