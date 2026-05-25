namespace TestProject;

public class CustomSerializerTests : CustomSerializerTestsBase
{
    public CustomSerializerTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithCustomSerializerAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .ConfigureServices(ConfigureSnakeCaseSerializer)
            .BuildAndStartAsync(TestCancellation);
}
