using Microsoft.Extensions.DependencyInjection;

namespace TestProject;

/// <summary>
/// Demonstrates the recommended workaround for capturing non-HTTP output bindings
/// ([QueueOutput], [BlobOutput]) declared alongside an HTTP trigger's [HttpResult] response, in
/// ASP.NET Core integration mode (ConfigureFunctionsWebApplication()). Unlike direct gRPC mode (see
/// HttpMixedOutputBindingTests in the non-AspNetCore test projects), GetOutputData() cannot capture
/// these outputs here because AspNetCoreForwardingHandler forwards the request directly to the
/// worker's in-memory TestServer without a corresponding gRPC InvocationResponse round trip.
///
/// Instead, the function records the values it would otherwise send to Queue/Blob through an
/// injected service (IProcessedItemsService), which the test overrides with a fresh in-memory
/// instance via ConfigureServices and asserts against after the HTTP call completes. This same
/// pattern works in both modes. See
/// https://github.com/bjorkstromm/azure-functions-test-framework/issues/142.
/// </summary>
public class HttpMixedOutputBindingWorkaroundTests : TestHostTestBase
{
    private readonly InMemoryProcessedItemsService _processedItems = new();

    public HttpMixedOutputBindingWorkaroundTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpMixedOutputBindingFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateHostBuilder)
            .ConfigureServices(services => services.AddSingleton<IProcessedItemsService>(_processedItems))
            .BuildAndStartAsync(TestCancellation);

    [Fact]
    public async Task CreateItemWithOutputs_RecordsQueueAndBlobOutputsViaInjectedService()
    {
        var response = await Client.PostAsync("/api/items-with-outputs", content: null, TestCancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal("created", body);

        // GetOutputData() is not populated in ASP.NET Core integration mode.
        Assert.Empty(response.GetOutputData());

        var recorded = _processedItems.TakeAll();
        Assert.Contains("QueueMessage:queued:created", recorded);
        Assert.Contains("BlobContent:blob:created", recorded);
    }
}
