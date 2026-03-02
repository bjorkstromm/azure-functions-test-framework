using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Net;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Core.Http;

/// <summary>
/// Maps gRPC InvocationResponse messages to HTTP test responses.
/// </summary>
public class HttpResponseMapper
{
    /// <summary>
    /// Extracts HTTP response information from an InvocationResponse.
    /// </summary>
    public HttpTestResponse MapToHttpResponse(StreamingMessage response)
    {
        if (response.InvocationResponse == null)
        {
            throw new ArgumentException("Message does not contain an InvocationResponse", nameof(response));
        }

        var invocationResponse = response.InvocationResponse;
        
        // Check if invocation succeeded
        if (invocationResponse.Result?.Status != StatusResult.Types.Status.Success)
        {
            return new HttpTestResponse
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Success = false,
                Error = invocationResponse.Result?.Exception?.Message ?? "Function invocation failed"
            };
        }

        // Extract HTTP response from return value or output bindings
        var httpData = GetHttpResponseData(invocationResponse);
        
        if (httpData == null)
        {
            return new HttpTestResponse
            {
                StatusCode = HttpStatusCode.OK,
                Success = true
            };
        }

        var testResponse = new HttpTestResponse
        {
            Success = true
        };

        // Parse status code
        if (!string.IsNullOrEmpty(httpData.StatusCode))
        {
            if (int.TryParse(httpData.StatusCode, out int statusCode))
            {
                testResponse.StatusCode = (HttpStatusCode)statusCode;
            }
        }

        // Copy headers
        foreach (var header in httpData.Headers)
        {
            testResponse.Headers[header.Key] = header.Value;
        }

        // Extract body
        if (httpData.Body != null)
        {
            testResponse.Body = ExtractBody(httpData.Body);
        }

        return testResponse;
    }

    private RpcHttp? GetHttpResponseData(InvocationResponse invocationResponse)
    {
        // Check return value first
        if (invocationResponse.ReturnValue?.Http != null)
        {
            return invocationResponse.ReturnValue.Http;
        }

        // Check output bindings
        var httpOutput = invocationResponse.OutputData
            .FirstOrDefault(p => p.Data?.Http != null);

        return httpOutput?.Data?.Http;
    }

    private string ExtractBody(TypedData data)
    {
        return data.DataCase switch
        {
            TypedData.DataOneofCase.String => data.String,
            TypedData.DataOneofCase.Json => data.Json,
            TypedData.DataOneofCase.Bytes => Convert.ToBase64String(data.Bytes.ToByteArray()),
            TypedData.DataOneofCase.Stream => Convert.ToBase64String(data.Stream.ToByteArray()),
            _ => string.Empty
        };
    }
}

/// <summary>
/// Represents an HTTP response from a function invocation.
/// </summary>
public class HttpTestResponse
{
    public bool Success { get; set; }
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string Body { get; set; } = string.Empty;
    public string? Error { get; set; }

    /// <summary>
    /// Deserializes the response body as JSON.
    /// </summary>
    public T? ReadFromJson<T>()
    {
        if (string.IsNullOrEmpty(Body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(Body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Checks if the response has the specified status code.
    /// </summary>
    public bool IsStatus(HttpStatusCode expected)
    {
        return StatusCode == expected;
    }

    /// <summary>
    /// Checks if the response is successful (2xx).
    /// </summary>
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode < 300;
}
