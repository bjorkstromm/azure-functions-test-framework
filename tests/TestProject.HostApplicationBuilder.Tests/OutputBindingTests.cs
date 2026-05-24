using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class OutputBindingTests : OutputBindingTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public OutputBindingTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(OutputBindingFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
