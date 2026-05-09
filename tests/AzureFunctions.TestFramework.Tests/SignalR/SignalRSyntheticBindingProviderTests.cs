using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.SignalR;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.SignalR;

/// <summary>
/// Unit tests for SignalR synthetic binding providers and builder extensions.
/// </summary>
public class SignalRSyntheticBindingProviderTests
{
    [Fact]
    public void SignalRConnectionInfoSyntheticBindingProvider_BindingType_ReturnsExpectedValue()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider("""{"url":"https://example"}""");

        Assert.Equal("signalRConnectionInfo", provider.BindingType);
    }

    [Fact]
    public void SignalRConnectionInfoSyntheticBindingProvider_StringConstructor_UsesProvidedJson()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider("""{"url":"https://example"}""");

        var binding = provider.CreateSyntheticParameter("connection", default);

        Assert.Equal("connection", binding.Name);
        Assert.Equal("""{"url":"https://example"}""", binding.Json);
    }

    [Fact]
    public void SignalRConnectionInfoSyntheticBindingProvider_UrlAndTokenConstructor_SerializesCamelCasePayload()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider("https://example", "token-1");

        var binding = provider.CreateSyntheticParameter("connection", default);

        using var document = JsonDocument.Parse(binding.Json!);
        Assert.Equal("https://example", document.RootElement.GetProperty("url").GetString());
        Assert.Equal("token-1", document.RootElement.GetProperty("accessToken").GetString());
    }

    [Fact]
    public void SignalRConnectionInfoSyntheticBindingProvider_NullJson_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SignalRConnectionInfoSyntheticBindingProvider((string)null!));
    }

    [Fact]
    public void SignalREndpointsSyntheticBindingProvider_BindingType_ReturnsExpectedValue()
    {
        var provider = new SignalREndpointsSyntheticBindingProvider([CreateEndpoint("primary", "https://primary")]);

        Assert.Equal("signalREndpoints", provider.BindingType);
    }

    [Fact]
    public void SignalREndpointsSyntheticBindingProvider_CreateSyntheticParameter_SerializesEndpoints()
    {
        var provider = new SignalREndpointsSyntheticBindingProvider(
        [
            CreateEndpoint("primary", "https://primary"),
            CreateEndpoint("secondary", "https://secondary")
        ]);

        var binding = provider.CreateSyntheticParameter("endpoints", default);

        using var document = JsonDocument.Parse(binding.Json!);
        Assert.Equal(2, document.RootElement.GetArrayLength());
        Assert.Equal("primary", document.RootElement[0].GetProperty("name").GetString());
        Assert.Equal("https://secondary", document.RootElement[1].GetProperty("endpoint").GetString());
    }

    [Fact]
    public void SignalREndpointsSyntheticBindingProvider_NullEndpoints_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SignalREndpointsSyntheticBindingProvider(null!));
    }

    [Fact]
    public void SignalRNegotiationSyntheticBindingProvider_BindingType_ReturnsExpectedValue()
    {
        var provider = new SignalRNegotiationSyntheticBindingProvider(CreateNegotiationContext());

        Assert.Equal("signalRNegotiation", provider.BindingType);
    }

    [Fact]
    public void SignalRNegotiationSyntheticBindingProvider_CreateSyntheticParameter_SerializesNegotiationContext()
    {
        var provider = new SignalRNegotiationSyntheticBindingProvider(CreateNegotiationContext());

        var binding = provider.CreateSyntheticParameter("negotiation", default);

        using var document = JsonDocument.Parse(binding.Json!);
        Assert.Equal("primary", document.RootElement.GetProperty("endpoints")[0].GetProperty("name").GetString());
        Assert.Equal(
            "https://client",
            document.RootElement.GetProperty("endpoints")[0].GetProperty("connectionInfo").GetProperty("url").GetString());
    }

    [Fact]
    public void SignalRNegotiationSyntheticBindingProvider_NullNegotiationContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SignalRNegotiationSyntheticBindingProvider(null!));
    }

    [Fact]
    public void WithSignalRConnectionInfo_ReturnsSameBuilderAndRegistersConnectionInfoProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSignalRConnectionInfo("https://example", "token-1");

        Assert.Same(builder, result);
        var provider = Assert.IsType<SignalRConnectionInfoSyntheticBindingProvider>(builder.LastSyntheticBindingProvider);
        using var document = JsonDocument.Parse(provider.CreateSyntheticParameter("connection", default).Json!);
        Assert.Equal("https://example", document.RootElement.GetProperty("url").GetString());
        Assert.Equal("token-1", document.RootElement.GetProperty("accessToken").GetString());
    }

    [Fact]
    public void WithSignalRConnectionInfo_ConnectionInfoOverload_NormalizesNullValuesToEmptyStrings()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSignalRConnectionInfo(new SignalRConnectionInfo());

        Assert.Same(builder, result);
        var provider = Assert.IsType<SignalRConnectionInfoSyntheticBindingProvider>(builder.LastSyntheticBindingProvider);
        using var document = JsonDocument.Parse(provider.CreateSyntheticParameter("connection", default).Json!);
        Assert.Equal(string.Empty, document.RootElement.GetProperty("url").GetString());
        Assert.Equal(string.Empty, document.RootElement.GetProperty("accessToken").GetString());
    }

    [Fact]
    public void WithSignalRNegotiation_ReturnsSameBuilderAndRegistersNegotiationProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSignalRNegotiation(CreateNegotiationContext());

        Assert.Same(builder, result);
        Assert.IsType<SignalRNegotiationSyntheticBindingProvider>(builder.LastSyntheticBindingProvider);
    }

    [Fact]
    public void WithSignalREndpoints_ReturnsSameBuilderAndRegistersEndpointsProvider()
    {
        var builder = new FakeBuilder();

        var result = builder.WithSignalREndpoints(CreateEndpoint("primary", "https://primary"));

        Assert.Same(builder, result);
        Assert.IsType<SignalREndpointsSyntheticBindingProvider>(builder.LastSyntheticBindingProvider);
    }

    [Fact]
    public void BuilderExtensions_NullArguments_Throw()
    {
        var builder = new FakeBuilder();

        Assert.Throws<ArgumentNullException>(() => FunctionsTestHostBuilderSignalRExtensions.WithSignalRConnectionInfo(null!, "url", "token"));
        Assert.Throws<ArgumentNullException>(() => builder.WithSignalRConnectionInfo(null!, "token"));
        Assert.Throws<ArgumentNullException>(() => builder.WithSignalRConnectionInfo("url", null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithSignalRConnectionInfo((SignalRConnectionInfo)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithSignalRNegotiation(null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithSignalREndpoints(null!));
    }

    private static SignalREndpoint CreateEndpoint(string name, string endpoint) => new()
    {
        Name = name,
        Endpoint = endpoint,
        EndpointType = SignalREndpointType.Primary,
        Online = true
    };

    private static SignalRNegotiationContext CreateNegotiationContext() => new()
    {
        Endpoints =
        [
            new SignalREndpointConnectionInfo
            {
                Name = "primary",
                Endpoint = "https://primary",
                EndpointType = SignalREndpointType.Primary,
                Online = true,
                ConnectionInfo = new SignalRConnectionInfo
                {
                    Url = "https://client",
                    AccessToken = "token-1"
                }
            }
        ]
    };

    private sealed class FakeBuilder : IFunctionsTestHostBuilder
    {
        public ISyntheticBindingProvider? LastSyntheticBindingProvider { get; private set; }

        public IFunctionsTestHostBuilder ConfigureServices(Action<Microsoft.Extensions.DependencyInjection.IServiceCollection> configure) => this;
        public IFunctionsTestHostBuilder WithFunctionsAssembly(System.Reflection.Assembly assembly) => this;
        public IFunctionsTestHostBuilder ConfigureSetting(string key, string value) => this;
        public IFunctionsTestHostBuilder ConfigureEnvironmentVariable(string name, string value) => this;
        public IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], Microsoft.Extensions.Hosting.IHostBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithHostApplicationBuilderFactory(Func<string[], Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder> factory) => this;
        public IFunctionsTestHostBuilder WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory) => this;
        public IFunctionsTestHostBuilder ConfigureWorkerLogging(Action<Microsoft.Extensions.Logging.ILoggingBuilder> configure) => this;
        public IFunctionsTestHostBuilder WithInvocationTimeout(TimeSpan timeout) => this;
        public IFunctionsTestHostBuilder WithSyntheticBindingProvider(ISyntheticBindingProvider provider)
        {
            LastSyntheticBindingProvider = provider;
            return this;
        }

        public IFunctionsTestHost Build() => throw new NotSupportedException();
    }
}
