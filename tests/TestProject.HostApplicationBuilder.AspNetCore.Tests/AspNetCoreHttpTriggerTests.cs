
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class AspNetCoreHttpTriggerTests : AspNetCoreHttpTriggerTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public AspNetCoreHttpTriggerTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
