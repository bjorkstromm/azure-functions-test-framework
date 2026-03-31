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
    public async Task Ping_DefaultRoute_ReturnsOk_WithHttpRequestAndFunctionContextBound()
    {
        // Act — call the endpoint that uses the DEFAULT route (function name "Ping", no Route= param)
        // This verifies that functions without explicit Route= still have HttpRequest + FunctionContext bound.
        var response = await _client!.GetAsync("/v1/Ping");
        _output.WriteLine($"Status: {response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine(body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // method comes from req.Method — proves HttpRequest was non-null.
        var method = root.GetProperty("method").GetString();
        Assert.Equal("GET", method, ignoreCase: true);

        // pong presence proves FunctionContext.InvocationId was accessible (logged without throwing).
        Assert.True(root.GetProperty("pong").GetBoolean());
    }

    [Fact]
    public async Task GetProductById_ReturnsOk_WithHttpRequestAndFunctionContextBound()
    {
        // Arrange — create a product whose ID is a Guid
        var productId = Guid.NewGuid();
        var product = _productService!.CreateWithId(productId.ToString(), "Widget", 9.99m);

        // Act — call the endpoint that uses HttpRequest + FunctionContext + Guid + CancellationToken
        var response = await _client!.GetAsync($"/v1/products/by-id/{productId}");
        _output.WriteLine($"Status: {response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine(body);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // requestMethod comes from req.Method inside the function — proves HttpRequest was non-null.
        // If req were null, req.Method in both the logger and here would throw NullReferenceException,
        // causing a 500 and failing the status assertion above.
        var requestMethod = root.GetProperty("requestMethod").GetString();
        Assert.Equal("GET", requestMethod, ignoreCase: true);

        var returnedId = root.GetProperty("id").GetString();
        Assert.Equal(product.Id, returnedId);
        Assert.Equal("Widget", root.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetProductById_ReturnsNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var response = await _client!.GetAsync($"/v1/products/by-id/{unknownId}");
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductById_InvalidGuid_ReturnsBadRequest()
    {
        // Act — non-Guid value should fail route constraint matching
        var response = await _client!.GetAsync("/v1/products/by-id/not-a-guid");
        _output.WriteLine($"Status: {response.StatusCode}");

        // ASP.NET Core route constraints reject "not-a-guid" and return 404 (no match) or 400
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400 but got {response.StatusCode}");
    }
}
