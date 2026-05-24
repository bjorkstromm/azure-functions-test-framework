using AzureFunctions.TestFramework.Durable;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class DurableTests : DurableTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public DurableTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .ConfigureFakeDurableSupport(typeof(DurableFunction).Assembly)
            .BuildAndStartAsync(TestCancellation);
}
