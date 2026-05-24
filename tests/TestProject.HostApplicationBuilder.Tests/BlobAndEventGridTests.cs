using Azure.Storage.Blobs;
using AzureFunctions.TestFramework.Blob;
using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Represents this type.
/// </summary>
public class BlobAndEventGridTests : BlobAndEventGridTestsBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    public BlobAndEventGridTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
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
