using AzureFunctions.TestFramework.SignalR;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.SignalR;

/// <summary>
/// Unit tests for <see cref="SignalRNegotiationSyntheticBindingProvider"/>.
/// </summary>
public class SignalRNegotiationSyntheticBindingProviderTests
{
    private static readonly JsonElement EmptyConfig =
        JsonDocument.Parse("{}").RootElement;

    // ── BindingType ───────────────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void BindingType_ReturnsSignalRNegotiation()
    {
        var provider = new SignalRNegotiationSyntheticBindingProvider(
            new SignalRNegotiationContext());

        Assert.Equal("signalRNegotiation", provider.BindingType);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_NullContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SignalRNegotiationSyntheticBindingProvider(null!));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_WithContext_SerializesToJson()
    {
        var context = new SignalRNegotiationContext
        {
            Endpoints =
            [
                new SignalREndpointConnectionInfo
                {
                    Endpoint = "https://ep1.signalr.net",
                    Name = "default",
                    Online = true,
                    ConnectionInfo = new SignalRConnectionInfo
                    {
                        Url = "https://ep1.signalr.net/client/?hub=chat",
                        AccessToken = "ep1-token"
                    }
                }
            ]
        };

        var provider = new SignalRNegotiationSyntheticBindingProvider(context);
        var result = provider.CreateSyntheticParameter("negotiation", EmptyConfig);

        Assert.NotNull(result!.Json);
        using var doc = JsonDocument.Parse(result.Json!);
        var endpoints = doc.RootElement.GetProperty("endpoints");
        Assert.Equal(1, endpoints.GetArrayLength());
        Assert.Equal("https://ep1.signalr.net",
            endpoints[0].GetProperty("endpoint").GetString());
        Assert.Equal("default",
            endpoints[0].GetProperty("name").GetString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_EmptyContext_SerializesToJson()
    {
        var context = new SignalRNegotiationContext();
        var provider = new SignalRNegotiationSyntheticBindingProvider(context);

        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        Assert.NotNull(result!.Json);
        // Must be valid JSON
        using var doc = JsonDocument.Parse(result.Json!);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    // ── CreateSyntheticParameter ──────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_ParameterNameIsPreserved()
    {
        var provider = new SignalRNegotiationSyntheticBindingProvider(
            new SignalRNegotiationContext());

        var result = provider.CreateSyntheticParameter("myNegotiation", EmptyConfig);

        Assert.Equal("myNegotiation", result!.Name);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_MultipleEndpoints_SerializesAll()
    {
        var context = new SignalRNegotiationContext
        {
            Endpoints =
            [
                new SignalREndpointConnectionInfo { Endpoint = "https://ep1.signalr.net", Name = "ep1" },
                new SignalREndpointConnectionInfo { Endpoint = "https://ep2.signalr.net", Name = "ep2" }
            ]
        };

        var provider = new SignalRNegotiationSyntheticBindingProvider(context);
        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        using var doc = JsonDocument.Parse(result!.Json!);
        var endpoints = doc.RootElement.GetProperty("endpoints");
        Assert.Equal(2, endpoints.GetArrayLength());
    }
}
