using Xunit;

namespace TestProject;

public abstract class HttpMixedOutputBindingTestsBase(ITestOutputHelper output) : TestHostTestBase(output)
{
    [Fact]
    public async Task CreateItemWithOutputs_CapturesQueueAndBlobOutputsAlongsideHttpResponse()
    {
        using var response = await Client.PostAsync("/api/items-with-outputs", content: null, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("created", await response.Content.ReadAsStringAsync(TestCancellation));
        AssertOutputs(response, "created");
    }

    [Fact]
    public async Task CreateItemWithOutputs_ResponseHeadersReadStillHasOutputs()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/items-with-outputs");
        request.Headers.Add("x-ms-invocation-id", Guid.NewGuid().ToString());
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestCancellation);
        AssertOutputs(response, "created");
        Assert.Equal("created", await response.Content.ReadAsStringAsync(TestCancellation));
    }

    [Fact]
    public async Task CreateItemWithOutputs_ConcurrentRequestsHaveIsolatedOutputs()
    {
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            var value = $"item-{i}";
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/items-with-outputs");
            request.Content = new StringContent(value);
            using var response = await Client.SendAsync(request, TestCancellation);
            AssertOutputs(response, value);
        }));
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundWithoutOutputs()
    {
        using var response = await Client.PostAsync("/api/no-such-function", content: null, TestCancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(response.GetOutputData());
    }

    protected static void AssertOutputs(HttpResponseMessage response, string value)
    {
        var outputData = response.GetOutputData();
        Assert.Equal(2, outputData.Count);
        Assert.Equal($"queued:{value}", outputData["QueueMessage"]?.ToString());
        Assert.Equal($"blob:{value}", outputData["BlobContent"]?.ToString());
    }

    // ── carl-berg's examples from issue #142 ─────────────────────────────────
    // https://github.com/bjorkstromm/azure-functions-test-framework/issues/142#issuecomment-6035594598

    [Fact]
    public async Task MyQueueFunction_QueueOutputOnReturnValue_IsCapturedUnderReturnKey()
    {
        using var response = await Client.PostAsync("/api/some-route", content: null, TestCancellation);

        Assert.True(response.IsSuccessStatusCode);
        var outputData = response.GetOutputData();
        Assert.Equal("Processed data", Assert.Single(outputData).Value?.ToString());
        Assert.True(outputData.ContainsKey("$return"));
    }

    [Fact]
    public async Task MyBlobAndQueueFunction_UploadsBlobAndCapturesQueueReturnValue()
    {
        using var response = await Client.PostAsync("/api/some-other-route", content: null, TestCancellation);

        Assert.True(response.IsSuccessStatusCode);
        var outputData = response.GetOutputData();
        Assert.Equal("Reference to uploaded blob", Assert.Single(outputData).Value?.ToString());

        var upload = Assert.Single(BlobUploads.Uploads);
        Assert.Equal("some-unique-name.json", upload.BlobName);
        Assert.Equal("Processed data", upload.Content);
    }

    /// <summary>
    /// The fake blob-upload transport shared with the concrete test class, so
    /// <see cref="MyBlobAndQueueFunction_UploadsBlobAndCapturesQueueReturnValue"/> can assert on
    /// what the function under test actually uploaded.
    /// </summary>
    protected abstract FakeBlobUploadTransport BlobUploads { get; }
}

public abstract class AspNetCoreHttpMixedOutputBindingTestsBase(ITestOutputHelper output)
    : HttpMixedOutputBindingTestsBase(output)
{
    [Fact]
    public async Task CreateItemWithAspNetCoreOutputs_CapturesOutputsAlongsideIActionResult()
    {
        using var response = await Client.PostAsync("/api/aspnetcore/items-with-outputs", content: null, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertOutputs(response, "created");
        Assert.Contains("created", await response.Content.ReadAsStringAsync(TestCancellation));
    }
}
