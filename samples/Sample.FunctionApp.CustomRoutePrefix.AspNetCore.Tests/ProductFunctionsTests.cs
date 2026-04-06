using System.Net;

namespace Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests;

/// <summary>
/// Sample tests for custom route prefix with ASP.NET Core / Kestrel mode.
/// </summary>
public class ProductFunctionsTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(ProductFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);

        _client = _testHost.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null) await _testHost.DisposeAsync();
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
    public async Task GetProductById_WithHttpRequest_ReturnsOk()
    {
        // Create a product first
        var createResponse = await _client!.PostAsJsonAsync("/v1/products", new { name = "Widget", price = 9.99m }, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(TestCancellation);
        var productId = created.GetProperty("id").GetString();

        // Verify HttpRequest + Guid route param binding works
        var response = await _client.GetAsync($"/v1/products/by-id/{productId}", TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthWithHttpRequest_ReturnsOk()
    {
        var response = await _client!.GetAsync("/v1/health-http-request", TestCancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Contains("healthy", body);
    }
}
