using AzureFunctions.TestFramework.Core.Grpc;
using Google.Protobuf.Collections;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="HttpTriggerMetadataHelper.PopulateTriggerMetadata"/>.
/// </summary>
public class HttpTriggerMetadataHelperTests
{
    private static MapField<string, TypedData> NewMap() => new();

    // ── Headers ───────────────────────────────────────────────────────────────

    [Fact]
    public void PopulateTriggerMetadata_NullHeaders_EmptyHeadersJson()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(map, null, null, null, null);

        Assert.True(map.ContainsKey("Headers"));
        Assert.Equal("{}", map["Headers"].Json);
    }

    [Fact]
    public void PopulateTriggerMetadata_WithHeaders_SerializesHeaders()
    {
        var map = NewMap();
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["X-Custom"] = "value"
        };
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(map, headers, null, null, null);

        var headersJson = map["Headers"].Json;
        Assert.Contains("Content-Type", headersJson);
        Assert.Contains("application/json", headersJson);
    }

    // ── Query params ──────────────────────────────────────────────────────────

    [Fact]
    public void PopulateTriggerMetadata_NullQueryParams_EmptyQueryJson()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(map, null, null, null, null);

        Assert.True(map.ContainsKey("Query"));
        Assert.Equal("{}", map["Query"].Json);
    }

    [Fact]
    public void PopulateTriggerMetadata_WithQueryParams_SerializesQuery()
    {
        var map = NewMap();
        var query = new Dictionary<string, string> { ["page"] = "2" };
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(map, null, query, null, null);

        var queryJson = map["Query"].Json;
        Assert.Contains("page", queryJson);
        Assert.Contains("2", queryJson);
    }

    // ── Body properties ───────────────────────────────────────────────────────

    [Fact]
    public void PopulateTriggerMetadata_JsonBodyWithStringProp_AddsStringEntry()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """{"name":"Alice"}""",
            contentType: "application/json");

        Assert.True(map.ContainsKey("name"));
        Assert.Equal("Alice", map["name"].String);
    }

    [Fact]
    public void PopulateTriggerMetadata_JsonBodyWithNumberProp_AddsStringEntry()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """{"age":30}""",
            contentType: "application/json");

        Assert.True(map.ContainsKey("age"));
        Assert.Equal("30", map["age"].String);
    }

    [Fact]
    public void PopulateTriggerMetadata_JsonBodyWithBoolProp_AddsStringEntry()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """{"active":true}""",
            contentType: "application/json");

        Assert.True(map.ContainsKey("active"));
        Assert.Equal("true", map["active"].String);
    }

    [Fact]
    public void PopulateTriggerMetadata_JsonBodyWithObjectProp_AddsJsonEntry()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """{"address":{"city":"NY"}}""",
            contentType: "application/json");

        Assert.True(map.ContainsKey("address"));
        Assert.Equal(TypedData.DataOneofCase.Json, map["address"].DataCase);
    }

    [Fact]
    public void PopulateTriggerMetadata_JsonBodyWithArrayProp_NotAdded()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """{"items":[1,2,3]}""",
            contentType: "application/json");

        Assert.False(map.ContainsKey("items"));
    }

    [Fact]
    public void PopulateTriggerMetadata_NonJsonContentType_BodyIgnored()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """{"name":"Alice"}""",
            contentType: "text/plain");

        // Headers and Query are added but no body properties
        Assert.False(map.ContainsKey("name"));
    }

    [Fact]
    public void PopulateTriggerMetadata_NullBody_BodyIgnored()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null, null, "application/json");

        // Headers and Query present but no extra keys
        Assert.DoesNotContain(map, kv => kv.Key != "Headers" && kv.Key != "Query");
    }

    [Fact]
    public void PopulateTriggerMetadata_InvalidJsonBody_DoesNotThrow()
    {
        var map = NewMap();
        // Should not throw; body properties silently skipped
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: "NOT-VALID-JSON",
            contentType: "application/json");
    }

    [Fact]
    public void PopulateTriggerMetadata_JsonBodyNotAnObject_NoExtraKeys()
    {
        var map = NewMap();
        // Root is an array, not an object
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """[1,2,3]""",
            contentType: "application/json");

        Assert.DoesNotContain(map, kv => kv.Key != "Headers" && kv.Key != "Query");
    }

    [Fact]
    public void PopulateTriggerMetadata_TextJsonContentType_BodyParsed()
    {
        var map = NewMap();
        HttpTriggerMetadataHelper.PopulateTriggerMetadata(
            map, null, null,
            body: """{"title":"test"}""",
            contentType: "text/json");

        Assert.True(map.ContainsKey("title"));
    }
}
