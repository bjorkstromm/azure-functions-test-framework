
namespace TestProject;

public class AspNetCoreCustomRoutePrefixTests : AspNetCoreCustomRoutePrefixTestsBase
{
    public AspNetCoreCustomRoutePrefixTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CustomRoutePrefix.HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(CrpTestHostFactory.CreateWebApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
