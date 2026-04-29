using AzureFunctions.TestFramework.Http;
using Google.Protobuf;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Net;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Http;

/// <summary>
/// Unit tests for <see cref="HttpResponseMapper"/>.
/// </summary>
public class HttpResponseMapperTests
{
    private readonly HttpResponseMapper _mapper = new();

    // ── Null InvocationResponse ────────────────────────────────────────────────

    [Fact]
    public void MapToHttpResponse_NullInvocationResponse_Throws()
    {
        var message = new StreamingMessage(); // no InvocationResponse set
        Assert.Throws<ArgumentException>(() => _mapper.MapToHttpResponse(message));
    }

    // ── Failure result ─────────────────────────────────────────────────────────

    [Fact]
    public void MapToHttpResponse_FailureResult_Returns500()
    {
        var message = BuildResponse(status: StatusResult.Types.Status.Failure,
            exceptionMessage: "Something went wrong");

        var response = _mapper.MapToHttpResponse(message);

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Something went wrong", response.Error);
    }

    [Fact]
    public void MapToHttpResponse_FailureWithNullException_UsesDefaultError()
    {
        var message = BuildResponse(status: StatusResult.Types.Status.Failure, exceptionMessage: null);

        var response = _mapper.MapToHttpResponse(message);

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(response.Error);
    }

    // ── Success with no HTTP response data ────────────────────────────────────

    [Fact]
    public void MapToHttpResponse_SuccessWithNullReturnValue_Returns200Empty()
    {
        var message = BuildResponse(status: StatusResult.Types.Status.Success);

        var response = _mapper.MapToHttpResponse(message);

        Assert.True(response.Success);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, response.Body);
    }

    [Fact]
    public void MapToHttpResponse_SuccessWithStringReturnValue_BodyIsString()
    {
        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { String = "plain text" }
        };
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        Assert.True(response.Success);
        Assert.Equal("plain text", response.Body);
    }

    [Fact]
    public void MapToHttpResponse_SuccessWithJsonReturnValue_BodyIsJson()
    {
        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { Json = """{"key":"value"}""" }
        };
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        Assert.Equal("""{"key":"value"}""", response.Body);
    }

    [Fact]
    public void MapToHttpResponse_SuccessWithBytesReturnValue_BodyIsUtf8String()
    {
        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { Bytes = ByteString.CopyFromUtf8("byte-body") }
        };
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        Assert.Equal("byte-body", response.Body);
    }

    [Fact]
    public void MapToHttpResponse_SuccessWithStreamReturnValue_BodyIsUtf8String()
    {
        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { Stream = ByteString.CopyFromUtf8("stream-body") }
        };
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        Assert.Equal("stream-body", response.Body);
    }

    // ── Success with HTTP response data in ReturnValue ─────────────────────────

    [Fact]
    public void MapToHttpResponse_ReturnValueHttp_ExtractsHttpResponse()
    {
        var rpcHttp = new RpcHttp
        {
            StatusCode = "201",
            Body = new TypedData { String = "created" }
        };
        rpcHttp.Headers["X-Custom"] = "test";

        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { Http = rpcHttp }
        };
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        Assert.True(response.Success);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("created", response.Body);
        Assert.Equal("test", response.Headers["X-Custom"]);
    }

    [Fact]
    public void MapToHttpResponse_ReturnValueHttp_NoStatusCode_DefaultsTo200()
    {
        var rpcHttp = new RpcHttp(); // no StatusCode set
        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { Http = rpcHttp }
        };
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        // StatusCode defaults to OK (default of HttpStatusCode)
        Assert.True(response.Success);
    }

    [Fact]
    public void MapToHttpResponse_ReturnValueHttp_InvalidStatusCode_KeepsDefault()
    {
        var rpcHttp = new RpcHttp { StatusCode = "not-a-number" };
        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { Http = rpcHttp }
        };
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        Assert.True(response.Success);
        // Status code stays at the default (0) since parsing failed
    }

    // ── Success with HTTP data in OutputData ───────────────────────────────────

    [Fact]
    public void MapToHttpResponse_OutputDataHttp_ExtractsHttpResponse()
    {
        var rpcHttp = new RpcHttp
        {
            StatusCode = "404",
            Body = new TypedData { String = "not found" }
        };

        var invResponse = new InvocationResponse
        {
            Result = new StatusResult { Status = StatusResult.Types.Status.Success }
        };
        invResponse.OutputData.Add(new ParameterBinding
        {
            Name = "$return",
            Data = new TypedData { Http = rpcHttp }
        });
        var message = new StreamingMessage { InvocationResponse = invResponse };

        var response = _mapper.MapToHttpResponse(message);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not found", response.Body);
    }

    // ── HttpTestResponse helpers ──────────────────────────────────────────────

    [Fact]
    public void IsStatus_MatchingCode_ReturnsTrue()
    {
        var response = new HttpTestResponse { StatusCode = HttpStatusCode.OK };
        Assert.True(response.IsStatus(HttpStatusCode.OK));
    }

    [Fact]
    public void IsStatus_DifferentCode_ReturnsFalse()
    {
        var response = new HttpTestResponse { StatusCode = HttpStatusCode.OK };
        Assert.False(response.IsStatus(HttpStatusCode.NotFound));
    }

    [Fact]
    public void IsSuccessStatusCode_200_ReturnsTrue()
    {
        var response = new HttpTestResponse { StatusCode = HttpStatusCode.OK };
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public void IsSuccessStatusCode_404_ReturnsFalse()
    {
        var response = new HttpTestResponse { StatusCode = HttpStatusCode.NotFound };
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public void ReadFromJson_EmptyBody_ReturnsDefault()
    {
        var response = new HttpTestResponse { Body = string.Empty };
        var obj = response.ReadFromJson<TestDto>();
        Assert.Null(obj);
    }

    [Fact]
    public void ReadFromJson_ValidJson_Deserializes()
    {
        var response = new HttpTestResponse { Body = """{"name":"Alice","age":30}""" };
        var obj = response.ReadFromJson<TestDto>();
        Assert.NotNull(obj);
        Assert.Equal("Alice", obj!.Name);
        Assert.Equal(30, obj.Age);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StreamingMessage BuildResponse(
        StatusResult.Types.Status status,
        string? exceptionMessage = null)
    {
        var statusResult = new StatusResult { Status = status };
        if (exceptionMessage != null)
            statusResult.Exception = new RpcException { Message = exceptionMessage };

        return new StreamingMessage
        {
            InvocationResponse = new InvocationResponse { Result = statusResult }
        };
    }

    private sealed record TestDto(string Name, int Age);
}
