using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests;

/// <summary>
/// Integration tests for <see cref="ProductFunctions"/> using <see cref="FunctionsTestHost"/> in
/// ASP.NET Core integration mode (<c>ConfigureFunctionsWebApplication</c>).
/// Uses <c>Program.CreateWorkerHostBuilder</c> so the worker starts a real Kestrel server;
/// <see cref="FunctionsTestHost.CreateHttpClient()"/> detects the HTTP port and forwards requests
/// through the full ASP.NET Core + Functions middleware pipeline.
/// Verifies that <c>HttpRequest</c>, <c>FunctionContext</c>, <c>Guid</c> route params and
/// <c>CancellationToken</c> are all bound correctly.
/// </summary>
public class ProductFunctionsTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;
    private InMemoryProductService? _productService;

    public ProductFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _productService = new InMemoryProductService();

        var loggerFactory = LoggerFactory.Create(b =>
            b.SetMinimumLevel(LogLevel.Information)
             .AddConsole());

        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(ProductFunctions).Assembly)
            .WithLoggerFactory(loggerFactory)
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .ConfigureServices(services =>
                services.AddSingleton<IProductService>(_productService));

        _testHost = await builder.BuildAndStartAsync();
        _client = _testHost.CreateHttpClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Fact]
    public async Task GetProductById_ReturnsOk_WithHttpRequestAndFunctionContextBound()
    {
        // Arrange — create a product whose ID is a Guid
        var productId = Guid.NewGuid();
        var product = _productService!.CreateWithId(productId.ToString(), "Widget", 9.99m);

        // Act — call the endpoint that uses HttpRequest + FunctionContext + Guid + CancellationToken
        var response = await _client!.GetAsync($"/v1/products/{productId}");
        _output.WriteLine($"Status: {response.StatusCode}");
        _output.WriteLine(await response.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var returned = JsonSerializer.Deserialize<Product>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(returned);
        Assert.Equal(product.Id, returned!.Id);
        Assert.Equal("Widget", returned.Name);
    }

    [Fact]
    public async Task GetProductById_ReturnsNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var response = await _client!.GetAsync($"/v1/products/{unknownId}");
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductById_InvalidGuid_ReturnsBadRequest()
    {
        // Act — non-Guid value should fail route constraint matching
        var response = await _client!.GetAsync("/v1/products/not-a-guid");
        _output.WriteLine($"Status: {response.StatusCode}");

        // ASP.NET Core route constraints reject "not-a-guid" and return 404 (no match) or 400
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400 but got {response.StatusCode}");
    }
}
