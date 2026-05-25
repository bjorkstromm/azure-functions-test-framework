using AzureFunctions.TestFramework.CosmosDB;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class CosmosDBTests : CosmosDBTestsBase
{
    public CosmosDBTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(CosmosDBFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .WithCosmosDBInputDocuments(
                CosmosDBFunction.DatabaseName,
                CosmosDBFunction.InputContainerName,
                new CosmosDocument { Id = "test-id", Title = CosmosDBInputTestTitle })
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
