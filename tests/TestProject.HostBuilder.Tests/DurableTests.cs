using AzureFunctions.TestFramework.Durable;

namespace TestProject;

public class DurableTests : DurableTestsBase
{
    public DurableTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DurableFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .ConfigureFakeDurableSupport(typeof(DurableFunction).Assembly)
            .BuildAndStartAsync(TestCancellation);
}
