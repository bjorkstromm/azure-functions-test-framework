namespace TestProject;

public class AspNetCoreCustomRoutePrefixTests : AspNetCoreCustomRoutePrefixTestsBase
{
    public AspNetCoreCustomRoutePrefixTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CustomRoutePrefix.HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
