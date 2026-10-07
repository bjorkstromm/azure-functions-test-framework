using Azure.Storage.Blobs;
using AzureFunctions.TestFramework.Blob;

namespace TestProject;

public class HttpMixedOutputBindingTests(ITestOutputHelper output) : AspNetCoreHttpMixedOutputBindingTestsBase(output)
{
    private readonly FakeBlobUploadTransport _blobUploads = new();

    protected override FakeBlobUploadTransport BlobUploads => _blobUploads;

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpMixedOutputBindingFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostApplicationBuilderFactory(TestHostFactory.CreateWebApplicationBuilder)
            .WithBlobServiceClient(new BlobServiceClient(
                new Uri("https://fakeaccount.blob.core.windows.net"),
                new BlobClientOptions { Transport = _blobUploads }))
            .WithBlobInputClient("blob-container-name")
            .BuildAndStartAsync(TestCancellation);
}
