using Xunit;

namespace TestProject;

/// <summary>Tests for the correlation middleware pipeline.</summary>
public abstract class MiddlewareTestsBase : TestHostTestBase
{
    protected MiddlewareTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationMiddleware.HeaderName, "test-correlation-id");

        var response = await Client.SendAsync(request, TestCancellation);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>(TestCancellation);
        Assert.NotNull(payload);
        Assert.Equal("test-correlation-id", payload.CorrelationId);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        var response = await Client.GetAsync("/api/correlation", TestCancellation);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>(TestCancellation);
        Assert.NotNull(payload);
        Assert.Null(payload.CorrelationId);
    }
}
