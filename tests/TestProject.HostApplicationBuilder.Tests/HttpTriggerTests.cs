
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class HttpTriggerTests : HttpTriggerTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public HttpTriggerTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
