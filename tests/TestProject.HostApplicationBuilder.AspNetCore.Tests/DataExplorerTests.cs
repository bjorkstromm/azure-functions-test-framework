using AzureFunctions.TestFramework.DataExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class DataExplorerTests : DataExplorerTestsBase
{
    public DataExplorerTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(DataExplorerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .WithKustoInputRows(
                DataExplorerFunction.DatabaseName,
                DataExplorerFunction.InputTableName,
                new KustoRow { Id = 99, Name = KustoInputTestName })
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
