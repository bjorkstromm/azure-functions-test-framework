using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class FrameworkFeaturesTests : FrameworkFeaturesTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public FrameworkFeaturesTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithServicesAsync(Action<IServiceCollection> configure) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .ConfigureServices(configure)
            .BuildAndStartAsync(TestCancellation);
}
