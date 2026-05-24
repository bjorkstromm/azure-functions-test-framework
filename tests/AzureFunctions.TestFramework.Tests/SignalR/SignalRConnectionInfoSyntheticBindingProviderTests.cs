using AzureFunctions.TestFramework.SignalR;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.SignalR;

/// <summary>
/// Unit tests for <see cref="SignalRConnectionInfoSyntheticBindingProvider"/>.
/// </summary>
public class SignalRConnectionInfoSyntheticBindingProviderTests
{
    private static readonly JsonElement EmptyConfig =
        JsonDocument.Parse("{}").RootElement;

    // ── BindingType ───────────────────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void BindingType_ReturnsSignalRConnectionInfo()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(
            url: "https://example.signalr.net",
            accessToken: "token");

        Assert.Equal("signalRConnectionInfo", provider.BindingType);
    }

    // ── Constructor(url, accessToken) ─────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_WithUrlAndToken_SerializesUrlAndToken()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(
            url: "https://example.signalr.net/client/?hub=chat",
            accessToken: "my-token");

        var result = provider.CreateSyntheticParameter("connInfo", EmptyConfig);

        Assert.NotNull(result);
        Assert.Equal("connInfo", result!.Name);
        using var doc = JsonDocument.Parse(result.Json!);
        Assert.Equal("https://example.signalr.net/client/?hub=chat",
            doc.RootElement.GetProperty("url").GetString());
        Assert.Equal("my-token",
            doc.RootElement.GetProperty("accessToken").GetString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_NullUrl_StillSerializesJson()
    {
        // The (url, accessToken) constructor delegates null handling to JsonSerializer;
        // null url serializes as JSON null rather than throwing.
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(url: null!, accessToken: "token");
        var result = provider.CreateSyntheticParameter("p", EmptyConfig);
        Assert.NotNull(result!.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_NullAccessToken_StillSerializesJson()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(url: "https://x", accessToken: null!);
        var result = provider.CreateSyntheticParameter("p", EmptyConfig);
        Assert.NotNull(result!.Json);
    }

    // ── Constructor(string json) ──────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_WithPreserializedJson_ReturnsSameJson()
    {
        var json = """{"url":"https://preserialized.example","accessToken":"pre-token"}""";
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(json);

        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        Assert.Equal(json, result!.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_NullJson_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SignalRConnectionInfoSyntheticBindingProvider((string)null!));
    }

    // ── CreateSyntheticParameter ──────────────────────────────────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_ParameterNameIsPreserved()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(
            url: "https://example.signalr.net", accessToken: "t");

        var result = provider.CreateSyntheticParameter("myParam", EmptyConfig);

        Assert.Equal("myParam", result!.Name);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_JsonIsNotNullOrEmpty()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(
            url: "https://example.signalr.net", accessToken: "t");

        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        Assert.NotNull(result!.Json);
        Assert.NotEmpty(result.Json!);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateSyntheticParameter_EmptyUrlAndToken_SerializesEmpty()
    {
        var provider = new SignalRConnectionInfoSyntheticBindingProvider(
            url: string.Empty, accessToken: string.Empty);

        var result = provider.CreateSyntheticParameter("p", EmptyConfig);

        using var doc = JsonDocument.Parse(result!.Json!);
        Assert.Equal(string.Empty, doc.RootElement.GetProperty("url").GetString());
        Assert.Equal(string.Empty, doc.RootElement.GetProperty("accessToken").GetString());
    }
}
