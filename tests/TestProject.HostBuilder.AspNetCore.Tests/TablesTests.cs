using AzureFunctions.TestFramework.Tables;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class TablesTests : TablesTestsBase
{
    public TablesTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TableInputFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .WithTableEntity(
                TableInputFunction.TableName,
                TableInputFunction.PartitionKey,
                TableInputFunction.RowKey,
                new CapturedTableEntity
                {
                    PartitionKey = TableInputFunction.PartitionKey,
                    RowKey = TableInputFunction.RowKey,
                    Payload = TableEntityTestPayload
                })
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
