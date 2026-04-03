using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sample.FunctionApp.CustomRoutePrefix.AspNetCore.Tests;

/// <summary>
/// Integration tests for <see cref="ProductFunctions"/> using <see cref="FunctionsTestHost"/> in
/// ASP.NET Core integration mode (<c>ConfigureFunctionsWebApplication</c>).
/// Uses <c>Program.CreateHostBuilder</c> so the worker starts a real Kestrel server;
/// <see cref="FunctionsTestHost.CreateHttpClient()"/> detects the HTTP port and forwards requests
/// through the full ASP.NET Core + Functions middleware pipeline.
/// Verifies that <c>HttpRequest</c>, <c>FunctionContext</c>, <c>Guid</c> route params and
/// <c>CancellationToken</c> are all bound correctly, as well as standard CRUD operations.
/// </summary>
public class ProductFunctionsTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;
    private InMemoryProductService? _productService;

    public ProductFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask InitializeAsync()
    {
        _productService = new InMemoryProductService();

        var loggerFactory = LoggerFactory.Create(b =>
            b.SetMinimumLevel(LogLevel.Information)
             .AddConsole());

        var builder = new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(ProductFunctions).Assembly)
            .WithLoggerFactory(loggerFactory)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .ConfigureServices(services =>
                services.AddSingleton<IProductService>(_productService));

        _testHost = await builder.BuildAndStartAsync(TestCancellation);
        _client = _testHost.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync(CancellationToken.None);
            _testHost.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Ping_DefaultRoute_ReturnsOk_WithHttpRequestAndFunctionContextBound()
    {
        // Act — call the endpoint that uses the DEFAULT route (function name "Ping", no Route= param)
        // This verifies that functions without explicit Route= still have HttpRequest + FunctionContext bound.
        var response = await _client!.GetAsync("/v1/Ping", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
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
        var response = await _client!.GetAsync($"/v1/products/by-id/{productId}", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
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
        var response = await _client!.GetAsync($"/v1/products/by-id/{unknownId}", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductById_InvalidGuid_ReturnsBadRequest()
    {
        // Act — non-Guid value should fail route constraint matching
        var response = await _client!.GetAsync("/v1/products/by-id/not-a-guid", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // ASP.NET Core route constraints reject "not-a-guid" and return 404 (no match) or 400
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404 or 400 but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetProducts_WithCustomRoutePrefix_ReturnsEmptyList()
    {
        // Act
        var response = await _client!.GetAsync("/v1/products", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreated_WithGeneratedId()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { name = "Widget", price = 9.99m });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client!.PostAsync("/v1/products", content, TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        var product = JsonSerializer.Deserialize<Product>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(product);
        Assert.False(string.IsNullOrEmpty(product!.Id));
        Assert.Equal("Widget", product.Name);
    }

    [Fact]
    public async Task GetProduct_ReturnsProduct_WhenExists()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { name = "Gadget", price = 19.99m });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/v1/products", content, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonSerializer.Deserialize<Product>(
            await createResponse.Content.ReadAsStringAsync(TestCancellation),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Act
        var response = await _client.GetAsync($"/v1/products/{created.Id}", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        var product = JsonSerializer.Deserialize<Product>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(product);
        Assert.Equal(created.Id, product!.Id);
    }

    [Fact]
    public async Task GetProduct_ReturnsNotFound_WhenProductDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync("/v1/products/nonexistent-id", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNoContent_WhenExists()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { name = "Disposable", price = 1.00m });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/v1/products", content, TestCancellation);
        var created = JsonSerializer.Deserialize<Product>(
            await createResponse.Content.ReadAsStringAsync(TestCancellation),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Act
        var response = await _client.DeleteAsync($"/v1/products/{created.Id}", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client!.DeleteAsync("/v1/products/nonexistent-id", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthWithHttpRequest_ReturnsOk_WhenUsingAspNetCoreHttpRequest()
    {
        // Act — this function uses HttpRequest (ASP.NET Core native) not HttpRequestData
        var response = await _client!.GetAsync("/v1/health-http-request", TestCancellation);
        _output.WriteLine($"Status: {response.StatusCode}");
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        _output.WriteLine(body);

        // Assert — verifies HttpRequest binding works through ConfigureFunctionsWebApplication
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("healthy", body);
        Assert.Contains("HttpRequest", body);
    }
}
