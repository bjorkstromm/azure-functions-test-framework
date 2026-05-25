
namespace TestProject;

public class HttpTriggerTests : HttpTriggerTestsBase
{
    public HttpTriggerTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .BuildAndStartAsync(TestCancellation);
}
