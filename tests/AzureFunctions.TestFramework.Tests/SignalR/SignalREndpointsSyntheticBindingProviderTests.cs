using AzureFunctions.TestFramework.SignalR;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.SignalR;

/// <summary>
/// Unit tests for <see cref="SignalREndpointsSyntheticBindingProvider"/>.
/// </summary>
public class SignalREndpointsSyntheticBindingProviderTests
{
    private static readonly JsonElement EmptyConfig =
        JsonDocument.Parse("{}").RootElement;

    // ── BindingType ───────────────────────────────────────────────────────────

    [Fact]
    public void BindingType_ReturnsSignalREndpoints()
    {
        var provider = new SignalREndpointsSyntheticBindingProvider([]);

        Assert.Equal("signalREndpoints", provider.BindingType);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullEndpoints_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SignalREndpointsSyntheticBindingProvider(null!));
    }

    [Fact]
    public void Constructor_EmptyEndpoints_SerializesToEmptyArray()
    {
        var provider = new SignalREndpointsSyntheticBindingProvider([]);

        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        Assert.NotNull(result!.Json);
        using var doc = JsonDocument.Parse(result.Json!);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void Constructor_WithEndpoints_SerializesAll()
    {
        var endpoints = new[]
        {
            new SignalREndpoint { Endpoint = "https://ep1.signalr.net", Name = "default-1" },
            new SignalREndpoint { Endpoint = "https://ep2.signalr.net", Name = "default-2" }
        };

        var provider = new SignalREndpointsSyntheticBindingProvider(endpoints);
        var result = provider.CreateSyntheticParameter("endpoints", EmptyConfig);

        using var doc = JsonDocument.Parse(result!.Json!);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("https://ep1.signalr.net",
            doc.RootElement[0].GetProperty("endpoint").GetString());
        Assert.Equal("default-1",
            doc.RootElement[0].GetProperty("name").GetString());
        Assert.Equal("https://ep2.signalr.net",
            doc.RootElement[1].GetProperty("endpoint").GetString());
    }

    // ── CreateSyntheticParameter ──────────────────────────────────────────────

    [Fact]
    public void CreateSyntheticParameter_ParameterNameIsPreserved()
    {
        var provider = new SignalREndpointsSyntheticBindingProvider([]);

        var result = provider.CreateSyntheticParameter("myEndpoints", EmptyConfig);

        Assert.Equal("myEndpoints", result!.Name);
    }

    [Fact]
    public void CreateSyntheticParameter_JsonIsNotNull()
    {
        var provider = new SignalREndpointsSyntheticBindingProvider([]);

        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        Assert.NotNull(result!.Json);
    }

    [Fact]
    public void CreateSyntheticParameter_SingleEndpoint_ContainsEndpointData()
    {
        var endpoints = new[]
        {
            new SignalREndpoint { Endpoint = "https://single.signalr.net", Name = "single-ep" }
        };

        var provider = new SignalREndpointsSyntheticBindingProvider(endpoints);
        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        using var doc = JsonDocument.Parse(result!.Json!);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("https://single.signalr.net",
            doc.RootElement[0].GetProperty("endpoint").GetString());
        Assert.Equal("single-ep",
            doc.RootElement[0].GetProperty("name").GetString());
    }
}
