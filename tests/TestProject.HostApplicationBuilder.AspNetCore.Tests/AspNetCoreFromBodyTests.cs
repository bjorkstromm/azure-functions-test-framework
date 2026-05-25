
namespace TestProject;

public class AspNetCoreFromBodyTests : AspNetCoreFromBodyTestsBase
{
    public AspNetCoreFromBodyTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(FromBodyFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
