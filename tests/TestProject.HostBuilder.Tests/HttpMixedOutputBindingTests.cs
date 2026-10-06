namespace TestProject;

/// <summary>
/// Verifies that non-HTTP output bindings ([QueueOutput], [BlobOutput]) declared alongside an
/// HTTP trigger's [HttpResult] response on a multi-output result type are captured and readable
/// via <see cref="FunctionsHttpOutputData.GetOutputData"/>, even though the function is invoked
/// through <see cref="IFunctionsTestHost.CreateHttpClient"/> rather than a non-HTTP trigger
/// extension method. Only applicable in direct gRPC mode
/// (<c>ConfigureFunctionsWorkerDefaults()</c>); see
/// https://github.com/bjorkstromm/azure-functions-test-framework/issues/142.
/// </summary>
public class HttpMixedOutputBindingTests : TestHostTestBase
{
    public HttpMixedOutputBindingTests(ITestOutputHelper output) : base(output) { }

    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(HttpMixedOutputBindingFunction).Assembly)
            .WithLoggerFactory(CreateLoggerFactory())
            .WithHostBuilderFactory(TestHostFactory.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);

    [Fact]
    public async Task CreateItemWithOutputs_CapturesQueueAndBlobOutputsAlongsideHttpResponse()
    {
        var response = await Client.PostAsync("/api/items-with-outputs", content: null, TestCancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal("created", body);

        var outputData = response.GetOutputData();
        Assert.Equal(2, outputData.Count);
        Assert.Equal("queued:created", outputData["QueueMessage"]?.ToString());
        Assert.Equal("blob:created", outputData["BlobContent"]?.ToString());
    }
}
