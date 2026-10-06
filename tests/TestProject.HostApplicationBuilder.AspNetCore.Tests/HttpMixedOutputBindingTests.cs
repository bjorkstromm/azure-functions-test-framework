namespace TestProject;

public class HttpMixedOutputBindingTests(ITestOutputHelper output) : AspNetCoreHttpMixedOutputBindingTestsBase(output)
{
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpMixedOutputBindingFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
