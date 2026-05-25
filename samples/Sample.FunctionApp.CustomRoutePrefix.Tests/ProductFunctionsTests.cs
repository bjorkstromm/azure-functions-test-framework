using AzureFunctions.TestFramework.Core;
using System.Net;

namespace Sample.FunctionApp.CustomRoutePrefix.Tests;

/// <summary>
/// Sample tests verifying custom <c>routePrefix = "v1"</c> from <c>host.json</c>.
/// </summary>
public class ProductFunctionsTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private IFunctionsTestHost? _host;
    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        _host = await FunctionsTestHost
            .CreateBuilder<Program>()
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);

        _client = _host.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_host != null) await _host.DisposeAsync();
    }

    [Fact]
    public async Task GetProducts_WithCustomRoutePrefix_ReturnsEmptyList()
    {
        var response = await _client!.GetAsync("/v1/products", TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task GetProducts_WithDefaultApiPrefix_ReturnsNotFound()
    {
        var response = await _client!.GetAsync("/api/products", TestCancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
