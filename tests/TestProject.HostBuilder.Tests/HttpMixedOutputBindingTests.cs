namespace TestProject;

public class HttpMixedOutputBindingTests(ITestOutputHelper output) : HttpMixedOutputBindingTestsBase(output)
{
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpMixedOutputBindingFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);
}
