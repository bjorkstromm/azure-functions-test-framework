using AzureFunctions.TestFramework.Tables;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class TablesTests : TablesTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public TablesTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TableInputFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
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
