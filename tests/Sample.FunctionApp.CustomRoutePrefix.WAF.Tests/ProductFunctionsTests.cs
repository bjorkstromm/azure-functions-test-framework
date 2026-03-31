using Sample.FunctionApp.CustomRoutePrefix.AspNetCore;

namespace Sample.FunctionApp.CustomRoutePrefix.WAF.Tests;

/// <summary>
/// Integration tests for <see cref="ProductFunctions"/> using <see cref="FunctionsWebApplicationFactory{TProgram}"/>.
/// Verifies that the framework correctly reads the custom <c>routePrefix = "v1"</c>
/// from <c>host.json</c> and routes requests accordingly when running via <c>ConfigureFunctionsWebApplication</c>.
/// </summary>
public class ProductFunctionsTests
    : IClassFixture<ProductFunctionsFixture>, IAsyncLifetime
{
    private readonly ProductFunctionsFixture _fixture;
    private HttpClient? _client;

    public ProductFunctionsTests(ProductFunctionsFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _client = _fixture.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetProducts_WithCustomRoutePrefix_ReturnsEmptyList()
    {
        // Act
        var response = await _client!.GetAsync("/v1/products");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreated_WithGeneratedId()
    {
        // Arrange
        var payload = JsonSerializer.Serialize(new { name = "Widget", price = 9.99m });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        // Act
        var response = await _client!.PostAsync("/v1/products", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
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
        var createResponse = await _client!.PostAsync("/v1/products", content);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonSerializer.Deserialize<Product>(
            await createResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Act
        var response = await _client.GetAsync($"/v1/products/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var product = JsonSerializer.Deserialize<Product>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        await Verify(product);
    }

    [Fact]
    public async Task GetProduct_ReturnsNotFound_WhenProductDoesNotExist()
    {
        // Act
        var response = await _client!.GetAsync("/v1/products/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNoContent_WhenExists()
    {
        // Arrange — create a product to delete
        var payload = JsonSerializer.Serialize(new { name = "Disposable", price = 1.00m });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/v1/products", content);
        var created = JsonSerializer.Deserialize<Product>(
            await createResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        // Act
        var response = await _client.DeleteAsync($"/v1/products/{created.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_ReturnsNotFound_WhenDoesNotExist()
    {
        // Act
        var response = await _client!.DeleteAsync("/v1/products/nonexistent-id");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
