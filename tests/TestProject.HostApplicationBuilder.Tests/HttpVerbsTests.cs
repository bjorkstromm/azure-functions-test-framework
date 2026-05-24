
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class HttpVerbsTests : HttpVerbsTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public HttpVerbsTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpVerbsFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
