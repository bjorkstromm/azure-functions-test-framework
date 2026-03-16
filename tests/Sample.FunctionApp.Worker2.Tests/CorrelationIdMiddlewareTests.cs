using AzureFunctions.TestFramework.Core;
using Sample.FunctionApp.Worker2;
using System.Net.Http.Json;
using Xunit;

namespace Sample.FunctionApp.Worker2.Tests;

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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "grpc-correlation-id");

        var response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        Assert.Equal("grpc-correlation-id", payload.CorrelationId);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        var response = await _client!.GetAsync("/api/correlation");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        Assert.NotNull(payload);
        Assert.Null(payload.CorrelationId);
    }
}
