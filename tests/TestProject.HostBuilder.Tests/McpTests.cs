using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class McpTests : McpTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public McpTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(McpToolTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
