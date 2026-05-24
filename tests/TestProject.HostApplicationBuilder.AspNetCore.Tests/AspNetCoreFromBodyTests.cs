
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class AspNetCoreFromBodyTests : AspNetCoreFromBodyTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public AspNetCoreFromBodyTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(FromBodyFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
