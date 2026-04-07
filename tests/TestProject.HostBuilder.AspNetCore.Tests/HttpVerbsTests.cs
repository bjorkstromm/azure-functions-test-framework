
namespace TestProject;

public class HttpVerbsTests : HttpVerbsTestsBase
{
    public HttpVerbsTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpVerbsFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
