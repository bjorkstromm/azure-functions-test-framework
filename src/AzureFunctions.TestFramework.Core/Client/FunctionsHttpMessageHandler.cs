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

            // 2. Find the function ID from the route (method + path)
            var functionId = FindFunctionIdFromRoute(method, path);
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

            // 4. Send to worker via gRPC (in-process)
            var grpcResponse = await _grpcHostService.SendMessageAsync(
                grpcRequest,
                cancellationToken
            );

            // 5. Convert gRPC response to HttpResponseMessage
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

    private string? FindFunctionIdFromRoute(string httpMethod, string path)
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

            if (string.Equals(normalizedPath, route, StringComparison.OrdinalIgnoreCase) ||
                MatchesRoutePattern(normalizedPath, route))
            {
                return kvp.Value;
            }
        }

        return null;
    }

    private bool MatchesRoutePattern(string path, string pattern)
    {
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathSegments.Length != patternSegments.Length)
        {
            return false;
        }

        for (int i = 0; i < pathSegments.Length; i++)
        {
            var patternSegment = patternSegments[i];
            var pathSegment = pathSegments[i];

            if (patternSegment.StartsWith('{') && patternSegment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(pathSegment, patternSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
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
