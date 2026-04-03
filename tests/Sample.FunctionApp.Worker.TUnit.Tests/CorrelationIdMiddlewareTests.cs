using AzureFunctions.TestFramework.Core;
using Microsoft.Extensions.Logging;
using Sample.FunctionApp.Worker;
using System.Net.Http.Json;
using TUnit.Core;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Integration tests for correlation id middleware in ASP.NET Core host mode.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    /// <summary>
    /// Starts a Kestrel-backed host before each test.
    /// </summary>
    [Before(Test)]
    public async Task SetUp()
    {
        // Arrange
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new TUnitLoggerProvider())))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync();

        _client = _testHost.CreateHttpClient();
    }

    /// <summary>
    /// Disposes the host after each test.
    /// </summary>
    [After(Test)]
    public async Task TearDown()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync();
            _testHost.Dispose();
        }
    }

    [Test]
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
        await Assert.That(payload).IsNotNull();
        await Verify(payload!);
    }

    [Test]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        // Act
        var response = await _client!.GetAsync("/api/correlation");

        // Assert
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>();
        await Assert.That(payload).IsNotNull();
        await Verify(payload!);
    }
}
