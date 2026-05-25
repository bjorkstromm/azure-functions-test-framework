
namespace TestProject;

public class AspNetCoreFromBodyTests : AspNetCoreFromBodyTestsBase
{
    public AspNetCoreFromBodyTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(FromBodyFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
