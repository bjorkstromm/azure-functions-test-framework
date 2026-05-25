namespace TestProject;

public class CustomSerializerTests : CustomSerializerTestsBase
{
    public CustomSerializerTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithCustomSerializerAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .ConfigureServices(ConfigureSnakeCaseSerializer)
            .BuildAndStartAsync(TestCancellation);
}
