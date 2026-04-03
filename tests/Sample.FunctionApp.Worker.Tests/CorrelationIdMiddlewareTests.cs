using AzureFunctions.TestFramework.Core;

namespace Sample.FunctionApp.Worker.Tests;

public class CorrelationIdMiddlewareTests : IAsyncLifetime
{
    private static CancellationToken TestCancellation => TestContext.Current.CancellationToken;

    private readonly ITestOutputHelper _output;
    private IFunctionsTestHost? _testHost;
    private HttpClient? _client;

    public CorrelationIdMiddlewareTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask InitializeAsync()
    {
        _testHost = await new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TodoFunctions).Assembly)
            .WithLoggerFactory(LoggerFactory.Create(b => b.AddProvider(new XUnitLoggerProvider(_output))))
            .WithHostBuilderFactory(Program.CreateHostBuilder)
            .BuildAndStartAsync(TestCancellation);

        _client = _testHost.CreateHttpClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_testHost != null)
        {
            await _testHost.StopAsync(TestCancellation);
            _testHost.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "grpc-correlation-id");

        // Act
        var response = await _client!.SendAsync(request, TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>(TestCancellation);
        Assert.NotNull(payload);
        await Verify(payload);
    }

    [Fact]
    public async Task CorrelationEndpoint_ReturnsNull_WhenHeaderMissing()
    {
        // Act
        var response = await _client!.GetAsync("/api/correlation", TestCancellation);

        // Assert
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>(TestCancellation);
        Assert.NotNull(payload);
        await Verify(payload);
    }
}
