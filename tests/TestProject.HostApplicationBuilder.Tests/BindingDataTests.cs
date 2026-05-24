
namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class BindingDataTests : BindingDataTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public BindingDataTests(ITestOutputHelper output) : base(output) { }

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
