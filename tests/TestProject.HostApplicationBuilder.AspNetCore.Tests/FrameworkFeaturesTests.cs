using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class FrameworkFeaturesTests : FrameworkFeaturesTestsBase
{
    public FrameworkFeaturesTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithServicesAsync(Action<IServiceCollection> configure) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .ConfigureServices(configure)
            .BuildAndStartAsync(TestCancellation);
}
