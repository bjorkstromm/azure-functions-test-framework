
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class WarmupTriggerTests : WarmupTriggerTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public WarmupTriggerTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(WarmupTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
