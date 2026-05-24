using Azure.Storage.Blobs;
using AzureFunctions.TestFramework.Blob;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

public class BlobAndEventGridTests : BlobAndEventGridTestsBase
{
    public BlobAndEventGridTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(InMemoryProcessedItemsService processedItems) =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(BlobTriggerFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateApplicationBuilder)
            .WithBlobInputContent(BlobInputTestPath, BinaryData.FromString(BlobInputTestContent))
            .WithBlobServiceClient(new BlobServiceClient("UseDevelopmentStorage=true"))
            .WithBlobInputClient(BlobInputClientTestPath)
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(processedItems))
            .BuildAndStartAsync(TestCancellation);
}
