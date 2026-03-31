using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Text;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Core.Http;

/// <summary>
/// Maps HTTP test requests to gRPC InvocationRequest messages.
/// </summary>
public class HttpRequestMapper
{
    /// <summary>
    /// Creates an InvocationRequest for an HTTP function.
    /// </summary>
    public StreamingMessage CreateInvocationRequest(
        string functionId,
        string method,
        string url,
        Dictionary<string, string>? headers = null,
        string? body = null,
        Dictionary<string, string>? queryParams = null,
        string bindingName = "req")
    {
        var invocationId = Guid.NewGuid().ToString();
        
        var httpRequest = new RpcHttp
        {
            Method = method.ToUpperInvariant(),
            Url = url
        };

        // Add headers
        if (headers != null)
        {
            foreach (var header in headers)
            {
                httpRequest.Headers.Add(header.Key, header.Value);
            }
        }

        // Add query parameters
        if (queryParams != null)
        {
            foreach (var param in queryParams)
            {
                httpRequest.Query.Add(param.Key, param.Value);
            }
        }

        // Add body
        if (!string.IsNullOrEmpty(body))
        {
            var bodyBytes = Google.Protobuf.ByteString.CopyFromUtf8(body);
            httpRequest.Body = new TypedData { Bytes = bodyBytes };
            httpRequest.RawBody = new TypedData { Bytes = bodyBytes };
        }

        var request = new StreamingMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            InvocationRequest = new InvocationRequest
            {
                InvocationId = invocationId,
                FunctionId = functionId,
                TraceContext = new RpcTraceContext
                {
                    TraceParent = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00",
                    TraceState = string.Empty
                }
            }
        };

        // Add HTTP trigger input using the actual binding parameter name from function metadata.
        request.InvocationRequest.InputData.Add(new ParameterBinding
        {
            Name = bindingName,
            Data = new TypedData
            {
                Http = httpRequest
            }
        });

        // Add trigger metadata
        request.InvocationRequest.TriggerMetadata.Add("sys.MethodName", new TypedData { String = method });
        request.InvocationRequest.TriggerMetadata.Add("sys.UtcNow", new TypedData { String = DateTime.UtcNow.ToString("o") });

        return request;
    }

    /// <summary>
    /// Creates an InvocationRequest with JSON body.
    /// </summary>
    public StreamingMessage CreateJsonInvocationRequest<T>(
        string functionId,
        string method,
        string url,
        T body,
        Dictionary<string, string>? headers = null,
        Dictionary<string, string>? queryParams = null)
    {
        var jsonBody = JsonSerializer.Serialize(body);
        
        var requestHeaders = new Dictionary<string, string>(headers ?? new Dictionary<string, string>())
        {
            ["Content-Type"] = "application/json"
        };

        return CreateInvocationRequest(functionId, method, url, requestHeaders, jsonBody, queryParams);
    }

    /// <summary>
    /// Extracts route parameters from a URL pattern and actual URL.
    /// </summary>
    public Dictionary<string, string> ExtractRouteParameters(string routeTemplate, string actualPath)
    {
        var parameters = new Dictionary<string, string>();
        
        var templateSegments = routeTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = actualPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < Math.Min(templateSegments.Length, pathSegments.Length); i++)
        {
            var segment = templateSegments[i];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                var paramName = segment.Trim('{', '}');
                parameters[paramName] = pathSegments[i];
            }
        }

        return parameters;
    }
}
