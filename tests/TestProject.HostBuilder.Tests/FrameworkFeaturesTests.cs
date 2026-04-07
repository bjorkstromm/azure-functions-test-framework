using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class FrameworkFeaturesTests : FrameworkFeaturesTestsBase
{
    public FrameworkFeaturesTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithServicesAsync(Action<IServiceCollection> configure) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .ConfigureServices(configure)
            .BuildAndStartAsync(TestCancellation);
}
