using AzureFunctions.TestFramework.Core;
using Sample.FunctionApp.Worker;
using System.Net.Http.Json;
using VerifyXunit;
using Xunit;

namespace Sample.FunctionApp.Worker.Tests;

public class CorrelationIdMiddlewareTests : IAsyncLifetime
{
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync();

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
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "grpc-correlation-id");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        await Verify(payload);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        // Act
        var response = await _client!.GetAsync("/api/correlation");

        // Assert
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        await Verify(payload);
    }
}
