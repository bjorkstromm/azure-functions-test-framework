namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class AspNetCoreCustomRoutePrefixTests : AspNetCoreCustomRoutePrefixTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public AspNetCoreCustomRoutePrefixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CustomRoutePrefix.HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
