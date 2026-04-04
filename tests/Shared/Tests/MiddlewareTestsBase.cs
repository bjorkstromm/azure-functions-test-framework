using Xunit;

namespace TestProject;

/// <summary>Tests for the correlation middleware pipeline.</summary>
public abstract class MiddlewareTestsBase : TestHostTestBase
{
    protected MiddlewareTestsBase(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Verifies that when the correlation header is absent the function returns a null
    /// correlation ID. Works in both direct gRPC and ASP.NET Core modes.
    /// </summary>
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
