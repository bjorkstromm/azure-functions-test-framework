
namespace TestProject;

public class FromBodyTests : FromBodyTestsBase
{
    public FromBodyTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(FromBodyFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
