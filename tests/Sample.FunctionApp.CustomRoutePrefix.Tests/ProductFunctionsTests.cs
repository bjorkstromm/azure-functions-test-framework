using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Sample.FunctionApp.CustomRoutePrefix.Tests;

/// <summary>
/// Integration tests for <see cref="ProductFunctions"/>.
/// Verifies that the framework correctly reads the custom <c>routePrefix = "v1"</c>
/// from <c>host.json</c> and routes requests accordingly.
/// </summary>
public class ProductFunctionsTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _host;
    private HttpClient? _client;

    public ProductFunctionsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask InitializeAsync()
    {
        _host = await FunctionsTestHost
            .CreateBuilder<Program>()
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateWorkerHostBuilder)
            .BuildAndStartAsync(TestCancellation);

        _client = _host.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_host != null) await _host.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetProducts_WithCustomRoutePrefix_ReturnsEmptyList()
    {
        // Act
        var response = await _client!.GetAsync("/v1/products", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task GetProducts_WithDefaultApiPrefix_ReturnsNotFound()
    {
        // Act — the old default prefix "api" must NOT resolve; validates the custom prefix is active
        var response = await _client!.GetAsync("/api/products", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreated_WithGeneratedId()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { name = "Widget", price = 9.99m });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        var response = await _client!.PostAsync("/v1/products", content, TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        var product = JsonSerializer.Deserialize<Product>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        await Verify(product);
    }

    [Fact]
    public async Task GetProduct_ReturnsProduct_WhenExists()
    {
        // Arrange — create a product first
        var payload = JsonSerializer.Serialize(new { name = "Gadget", price = 19.99m });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/v1/products", content, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonSerializer.Deserialize<Product>(
            await createResponse.Content.ReadAsStringAsync(TestCancellation),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Act
        var response = await _client.GetAsync($"/v1/products/{created.Id}", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestCancellation);
        var product = JsonSerializer.Deserialize<Product>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        await Verify(product);
    }

    [Fact]
    public async Task GetProduct_ReturnsNotFound_WhenProductDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync("/v1/products/nonexistent-id", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNoContent_WhenExists()
    {
        // Arrange — create a product to delete
        var payload = JsonSerializer.Serialize(new { name = "Disposable", price = 1.00m });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/v1/products", content, TestCancellation);
        var created = JsonSerializer.Deserialize<Product>(
            await createResponse.Content.ReadAsStringAsync(TestCancellation),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Act
        var response = await _client.DeleteAsync($"/v1/products/{created.Id}", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client!.DeleteAsync("/v1/products/nonexistent-id", TestCancellation);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
