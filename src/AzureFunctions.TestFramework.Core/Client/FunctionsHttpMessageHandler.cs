using AzureFunctions.TestFramework.Core.Grpc;
using AzureFunctions.TestFramework.Core.Http;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Net;

namespace AzureFunctions.TestFramework.Core.Client;

/// <summary>
/// Custom HttpMessageHandler that intercepts HTTP requests and routes them
/// to Azure Functions via gRPC in-process. Similar to how TestServer works in ASP.NET Core.
/// </summary>
public class FunctionsHttpMessageHandler : HttpMessageHandler
{
    private readonly GrpcHostService _grpcHostService;
    private readonly HttpRequestMapper _requestMapper;
    private readonly HttpResponseMapper _responseMapper;
    private readonly Dictionary<string, string> _routeToFunctionMap;

    public FunctionsHttpMessageHandler(
        GrpcHostService grpcHostService,
        Dictionary<string, string> routeToFunctionMap)
    {
        _grpcHostService = grpcHostService;
        _requestMapper = new HttpRequestMapper();
        _responseMapper = new HttpResponseMapper();
        _routeToFunctionMap = routeToFunctionMap;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Extract request information
            var method = request.Method.Method;
            var path = request.RequestUri?.PathAndQuery ?? "/";
            var headers = ExtractHeaders(request);
            var body = await ReadBodyAsync(request, cancellationToken);
            var queryParams = ExtractQueryParameters(request);

            // 2. Find the function ID and route parameters from the route (method + path)
            var (functionId, routeParams) = FindFunctionMatch(method, path);
            if (string.IsNullOrEmpty(functionId))
            {
                return CreateNotFoundResponse($"No function found for route: {method} {path}");
            }

            // 3. Create gRPC InvocationRequest
            var grpcRequest = _requestMapper.CreateInvocationRequest(
                functionId: functionId,
                method: method,
                url: path,
                headers: headers,
                body: body,
                queryParams: queryParams
            );

            // 4. Add route parameters to InputData so the worker can bind them to function
            //    parameters (e.g. "string id" in GetTodo([HttpTrigger] HttpRequestData req, string id)).
            foreach (var (paramName, paramValue) in routeParams)
            {
                grpcRequest.InvocationRequest.InputData.Add(new ParameterBinding
                {
                    Name = paramName,
                    Data = new TypedData { String = paramValue }
                });
            }

            // 5. Send to worker via gRPC (in-process)
            var grpcResponse = await _grpcHostService.SendMessageAsync(
                grpcRequest,
                cancellationToken
            );

            // 6. Convert gRPC response to HttpResponseMessage
            var testResponse = _responseMapper.MapToHttpResponse(grpcResponse);
            return CreateHttpResponseMessage(testResponse);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(ex);
        }
    }

    private Dictionary<string, string> ExtractHeaders(HttpRequestMessage request)
    {
        var headers = new Dictionary<string, string>();

        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        if (request.Content?.Headers != null)
        {
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(", ", header.Value);
            }
        }

        return headers;
    }

    private async Task<string?> ReadBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content == null)
        {
            return null;
        }

        return await request.Content.ReadAsStringAsync(cancellationToken);
    }

    private Dictionary<string, string> ExtractQueryParameters(HttpRequestMessage request)
    {
        var queryParams = new Dictionary<string, string>();

        if (request.RequestUri?.Query == null)
        {
            return queryParams;
        }

        var query = request.RequestUri.Query.TrimStart('?');
        if (string.IsNullOrEmpty(query))
        {
            return queryParams;
        }

        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                queryParams[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
            }
        }

        return queryParams;
    }

    private (string? FunctionId, IReadOnlyDictionary<string, string> RouteParams) FindFunctionMatch(
        string httpMethod, string path)
    {
        // Normalize the path (strip leading slash and "api/" prefix)
        var normalizedPath = path.TrimStart('/');
        if (normalizedPath.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath.Substring(4);
        }
        // Strip query string
        var queryIndex = normalizedPath.IndexOf('?');
        if (queryIndex >= 0)
        {
            normalizedPath = normalizedPath.Substring(0, queryIndex);
        }

        var upperMethod = httpMethod.ToUpperInvariant();
        var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Try exact match first, then pattern match
        foreach (var kvp in _routeToFunctionMap)
        {
            // Keys are in format "METHOD:route"
            var colonIdx = kvp.Key.IndexOf(':');
            if (colonIdx < 0) continue;

            var keyMethod = kvp.Key.Substring(0, colonIdx);
            var keyRoute = kvp.Key.Substring(colonIdx + 1);

            if (!string.Equals(keyMethod, upperMethod, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var route = keyRoute.TrimStart('/');
            if (route.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            {
                route = route.Substring(4);
            }

            // Exact match — no route parameters to extract.
            if (string.Equals(normalizedPath, route, StringComparison.OrdinalIgnoreCase))
            {
                return (kvp.Value, System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty);
            }

            // Pattern match: route segments may contain {param} placeholders.
            var routeSegments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (routeSegments.Length != pathSegments.Length)
            {
                continue;
            }

            var match = true;
            for (int i = 0; i < routeSegments.Length; i++)
            {
                var seg = routeSegments[i];
                if (seg.StartsWith('{') && seg.EndsWith('}'))
                {
                    // Parameter placeholder — matches any value.
                    continue;
                }

                if (!string.Equals(pathSegments[i], seg, StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                // Extract route parameter values (e.g. "{id}" → pathSegments[i]).
                var routeParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < routeSegments.Length; i++)
                {
                    var seg = routeSegments[i];
                    if (seg.Length > 2 && seg[0] == '{' && seg[seg.Length - 1] == '}')
                    {
                        routeParams[seg.Substring(1, seg.Length - 2)] = pathSegments[i];
                    }
                }
                return (kvp.Value, routeParams);
            }
        }

        return (null, System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty);
    }

    private HttpResponseMessage CreateHttpResponseMessage(HttpTestResponse testResponse)
    {
        var body = testResponse.Success ? testResponse.Body : (testResponse.Error ?? testResponse.Body);
        var response = new HttpResponseMessage(testResponse.StatusCode)
        {
            Content = new StringContent(body)
        };

        foreach (var header in testResponse.Headers)
        {
            if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return response;
    }

    private HttpResponseMessage CreateNotFoundResponse(string message)
    {
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(message)
        };
    }

    private HttpResponseMessage CreateErrorResponse(Exception ex)
    {
        return new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent($"Error invoking function: {ex.Message}")
        };
    }
}
