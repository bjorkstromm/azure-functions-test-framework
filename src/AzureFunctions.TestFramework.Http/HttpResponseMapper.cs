using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Net;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Http;

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
            var body = GetDirectReturnValueBody(invocationResponse);

            return new HttpTestResponse
            {
                StatusCode = HttpStatusCode.OK,
                Success = true,
                Body = body
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

    private string GetDirectReturnValueBody(InvocationResponse invocationResponse)
    {
        if (invocationResponse.ReturnValue == null)
        {
            return string.Empty;
        }

        return ExtractBody(invocationResponse.ReturnValue);
    }

    private string ExtractBody(TypedData data)
    {
        return data.DataCase switch
        {
            TypedData.DataOneofCase.String => data.String,
            TypedData.DataOneofCase.Json => data.Json,
            TypedData.DataOneofCase.Bytes => System.Text.Encoding.UTF8.GetString(data.Bytes.ToByteArray()),
            TypedData.DataOneofCase.Stream => System.Text.Encoding.UTF8.GetString(data.Stream.ToByteArray()),
            _ => string.Empty
        };
    }
}

/// <summary>
/// Represents an HTTP response from a function invocation.
/// </summary>
public class HttpTestResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the invocation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code.
    /// </summary>
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>
    /// Gets or sets the HTTP response headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the response body.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message when the invocation fails.
    /// </summary>
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
