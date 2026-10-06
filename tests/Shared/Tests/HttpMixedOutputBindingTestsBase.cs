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
