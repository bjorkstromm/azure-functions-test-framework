using AzureFunctions.TestFramework.Http;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Http;

/// <summary>
/// Unit tests for <see cref="HttpRequestMapper.CreateInvocationRequest"/>
/// and <see cref="HttpRequestMapper.CreateJsonInvocationRequest{T}"/>.
/// </summary>
public class HttpRequestMapperCreateRequestTests
{
    private readonly HttpRequestMapper _mapper = new();

    // ── CreateInvocationRequest — basic ───────────────────────────────────────

    [Fact]
    public void CreateInvocationRequest_MinimalArgs_BuildsValidRequest()
    {
        var message = _mapper.CreateInvocationRequest("fn-1", "GET", "http://localhost/api/todos");

        Assert.NotNull(message.InvocationRequest);
        Assert.Equal("fn-1", message.InvocationRequest.FunctionId);
        Assert.NotEmpty(message.InvocationRequest.InvocationId);
        Assert.NotEmpty(message.RequestId);
    }

    [Fact]
    public void CreateInvocationRequest_Method_NormalisedToUpperCase()
    {
        var message = _mapper.CreateInvocationRequest("fn-1", "post", "http://localhost/api/todos");
        var httpBinding = message.InvocationRequest.InputData
            .FirstOrDefault(p => p.Data?.Http != null);

        Assert.NotNull(httpBinding);
        Assert.Equal("POST", httpBinding!.Data.Http.Method);
    }

    [Fact]
    public void CreateInvocationRequest_WithHeaders_AddsHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token",
            ["X-Correlation-Id"] = "abc123"
        };
        var message = _mapper.CreateInvocationRequest(
            "fn-1", "GET", "http://localhost/api/todos", headers);

        var http = message.InvocationRequest.InputData
            .First(p => p.Data?.Http != null).Data.Http;

        Assert.Equal("Bearer token", http.Headers["Authorization"]);
        Assert.Equal("abc123", http.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public void CreateInvocationRequest_WithQueryParams_AddsQueryParams()
    {
        var query = new Dictionary<string, string> { ["page"] = "2", ["size"] = "10" };
        var message = _mapper.CreateInvocationRequest(
            "fn-1", "GET", "http://localhost/api/todos",
            queryParams: query);

        var http = message.InvocationRequest.InputData
            .First(p => p.Data?.Http != null).Data.Http;

        Assert.Equal("2", http.Query["page"]);
        Assert.Equal("10", http.Query["size"]);
    }

    [Fact]
    public void CreateInvocationRequest_WithBody_SetsBodyAndRawBody()
    {
        var message = _mapper.CreateInvocationRequest(
            "fn-1", "POST", "http://localhost/api/todos",
            body: """{"title":"Buy milk"}""");

        var http = message.InvocationRequest.InputData
            .First(p => p.Data?.Http != null).Data.Http;

        Assert.NotNull(http.Body);
        Assert.NotNull(http.RawBody);
        Assert.Equal(
            """{"title":"Buy milk"}""",
            System.Text.Encoding.UTF8.GetString(http.Body.Bytes.ToByteArray()));
    }

    [Fact]
    public void CreateInvocationRequest_NoBody_DoesNotSetBody()
    {
        var message = _mapper.CreateInvocationRequest(
            "fn-1", "GET", "http://localhost/api/todos");

        var http = message.InvocationRequest.InputData
            .First(p => p.Data?.Http != null).Data.Http;

        Assert.Equal(TypedData.DataOneofCase.None, http.Body?.DataCase ?? TypedData.DataOneofCase.None);
    }

    [Fact]
    public void CreateInvocationRequest_AddsSysTriggerMetadata()
    {
        var message = _mapper.CreateInvocationRequest(
            "fn-1", "GET", "http://localhost/api/todos");

        var meta = message.InvocationRequest.TriggerMetadata;
        Assert.True(meta.ContainsKey("sys.MethodName"));
        Assert.True(meta.ContainsKey("sys.UtcNow"));
        Assert.Equal("GET", meta["sys.MethodName"].String);
    }

    [Fact]
    public void CreateInvocationRequest_WithContentTypeHeader_PopulatesMetadata()
    {
        var headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" };
        var message = _mapper.CreateInvocationRequest(
            "fn-1", "POST", "http://localhost/api/todos",
            headers: headers,
            body: """{"name":"Alice"}""");

        var meta = message.InvocationRequest.TriggerMetadata;
        // Body properties should be parsed
        Assert.True(meta.ContainsKey("name"));
    }

    [Fact]
    public void CreateInvocationRequest_CustomBindingName_UsedInInputData()
    {
        var message = _mapper.CreateInvocationRequest(
            "fn-1", "GET", "http://localhost/api/todos",
            bindingName: "request");

        var binding = message.InvocationRequest.InputData
            .FirstOrDefault(p => p.Name == "request");

        Assert.NotNull(binding);
    }

    // ── CreateJsonInvocationRequest ───────────────────────────────────────────

    [Fact]
    public void CreateJsonInvocationRequest_SetsContentTypeAndSerializesBody()
    {
        var body = new { title = "Buy milk", done = false };
        var message = _mapper.CreateJsonInvocationRequest(
            "fn-1", "POST", "http://localhost/api/todos", body);

        var http = message.InvocationRequest.InputData
            .First(p => p.Data?.Http != null).Data.Http;

        Assert.Equal("application/json", http.Headers["Content-Type"]);
        var rawBody = System.Text.Encoding.UTF8.GetString(http.Body.Bytes.ToByteArray());
        Assert.Contains("Buy milk", rawBody);
    }

    [Fact]
    public void CreateJsonInvocationRequest_MergesExtraHeaders()
    {
        var body = new { title = "Buy milk" };
        var extraHeaders = new Dictionary<string, string> { ["X-Custom"] = "abc" };
        var message = _mapper.CreateJsonInvocationRequest(
            "fn-1", "POST", "http://localhost/api/todos", body, extraHeaders);

        var http = message.InvocationRequest.InputData
            .First(p => p.Data?.Http != null).Data.Http;

        Assert.Equal("application/json", http.Headers["Content-Type"]);
        Assert.Equal("abc", http.Headers["X-Custom"]);
    }
}
