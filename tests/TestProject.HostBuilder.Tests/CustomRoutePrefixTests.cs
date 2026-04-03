
namespace TestProject;

public class CustomRoutePrefixTests : CustomRoutePrefixTestsBase
{
    public CustomRoutePrefixTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CustomRoutePrefix.HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(CrpTestHostFactory.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
