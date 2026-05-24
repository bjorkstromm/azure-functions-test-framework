using AzureFunctions.TestFramework.DataExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class DataExplorerTests : DataExplorerTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public DataExplorerTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DataExplorerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .WithKustoInputRows(
                DataExplorerFunction.DatabaseName,
                DataExplorerFunction.InputTableName,
                new KustoRow { Id = 99, Name = KustoInputTestName })
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
