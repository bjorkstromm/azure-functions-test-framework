using Xunit;

namespace TestProject;

/// <summary>Tests verifying GET/HEAD/OPTIONS/PATCH routing.</summary>
public abstract class HttpVerbsTestsBase : TestHostTestBase
{
    protected HttpVerbsTestsBase(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("GET", "probe", false)]
    [InlineData("HEAD", "", false)]
    [InlineData("OPTIONS", "", false)]
    [InlineData("PATCH", "PATCH", true)]
    public async Task HttpVerbsProbe_RoutesVerbAndExposesMethodHeader(string method, string expectedBody, bool sendBody)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/http-verbs-probe");
        if (sendBody) request.Content = new StringContent(method);

        var response = await Client.SendAsync(request, TestCancellation);
        response.EnsureSuccessStatusCode();

        Assert.Equal(method, response.Headers.GetValues("X-Probe-Method").First());
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal(expectedBody, body);
    }
}
