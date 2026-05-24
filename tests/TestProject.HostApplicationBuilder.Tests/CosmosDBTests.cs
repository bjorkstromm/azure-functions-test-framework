using AzureFunctions.TestFramework.CosmosDB;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class CosmosDBTests : CosmosDBTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public CosmosDBTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CosmosDBFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .WithCosmosDBInputDocuments(
                CosmosDBFunction.DatabaseName,
                CosmosDBFunction.InputContainerName,
                new CosmosDocument { Id = "test-id", Title = CosmosDBInputTestTitle })
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
