
namespace TestProject;

public class AspNetCoreHttpTriggerTests : AspNetCoreHttpTriggerTestsBase
{
    public AspNetCoreHttpTriggerTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
