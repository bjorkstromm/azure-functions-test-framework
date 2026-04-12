using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests that verify the test framework correctly handles function apps that use a custom
/// <see cref="JsonSerializerOptions"/> (e.g. snake_case property naming).
/// </summary>
/// <remarks>
/// Azure Functions dotnet-isolated lets callers configure serialization via
/// <c>services.Configure&lt;WorkerOptions&gt;(opts => opts.Serializer = new JsonObjectSerializer(...))</c>.
/// These tests verify that the framework correctly forwards request bodies to the worker unchanged (as raw
/// bytes) and returns the worker's serialized response to the test, so that both the custom naming
/// policy and custom converters are respected end-to-end.
/// </remarks>
public abstract class CustomSerializerTestsBase : TestHostTestBase
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    protected CustomSerializerTestsBase(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Creates the test host with a snake_case <see cref="JsonSerializerOptions"/> applied
    /// via <c>services.Configure&lt;WorkerOptions&gt;(opts => opts.Serializer = ...)</c>.
    /// </summary>
    protected abstract Task<IFunctionsTestHost> CreateTestHostWithCustomSerializerAsync();

    // Override so the abstract base type does not require a separate default host
    protected override Task<IFunctionsTestHost> CreateTestHostAsync() =>
        CreateTestHostWithCustomSerializerAsync();

    /// <summary>
    /// Configures services to use a snake_case <see cref="JsonObjectSerializer"/>.
    /// Concrete test classes pass this delegate to
    /// <see cref="IFunctionsTestHostBuilder.ConfigureServices"/>.
    /// </summary>
    protected static void ConfigureSnakeCaseSerializer(IServiceCollection services) =>
        services.Configure<WorkerOptions>(opts =>
            opts.Serializer = new JsonObjectSerializer(SnakeCaseOptions));

    /// <summary>
    /// Verifies that the worker serializes the response using the configured snake_case naming policy.
    /// </summary>
    [Fact]
    public async Task CustomSerializer_ResponseUsesCustomNamingPolicy()
    {
        // Arrange
        await using var testHost = await CreateTestHostWithCustomSerializerAsync();
        using var client = testHost.CreateHttpClient();

        var request = new { product_id = "p-1", product_name = "Widget", is_active = true };
        using var requestContent = JsonContent.Create(request, options: SnakeCaseOptions);

        // Act
        using var response = await client.PostAsync("/api/echo-product", requestContent, TestCancellation);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(TestCancellation);

        // Assert: response body must use snake_case names from the custom serializer
        Assert.Contains("\"product_id\"", responseJson);
        Assert.Contains("\"product_name\"", responseJson);
        Assert.Contains("\"is_active\"", responseJson);

        // Verify values are preserved correctly
        var result = JsonSerializer.Deserialize<ProductResponse>(responseJson, SnakeCaseOptions);
        Assert.NotNull(result);
        Assert.Equal("p-1", result.ProductId);
        Assert.Equal("Widget", result.ProductName);
        Assert.True(result.IsActive);
    }

    /// <summary>
    /// Verifies that PascalCase (default) property names are NOT present in the response when
    /// a snake_case naming policy is configured.
    /// </summary>
    [Fact]
    public async Task CustomSerializer_ResponseDoesNotUsePascalCase()
    {
        await using var testHost = await CreateTestHostWithCustomSerializerAsync();
        using var client = testHost.CreateHttpClient();

        var request = new { product_id = "p-2", product_name = "Gadget", is_active = false };
        using var requestContent = JsonContent.Create(request, options: SnakeCaseOptions);

        using var response = await client.PostAsync("/api/echo-product", requestContent, TestCancellation);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(TestCancellation);

        // PascalCase keys must be absent when snake_case policy is active
        Assert.DoesNotContain("\"ProductId\"", responseJson);
        Assert.DoesNotContain("\"ProductName\"", responseJson);
        Assert.DoesNotContain("\"IsActive\"", responseJson);
    }
}
