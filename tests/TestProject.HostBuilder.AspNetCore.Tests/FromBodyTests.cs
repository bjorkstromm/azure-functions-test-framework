
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class FromBodyTests : FromBodyTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public FromBodyTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(FromBodyFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
