
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class MiddlewareTests : MiddlewareTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public MiddlewareTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(MiddlewareFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
