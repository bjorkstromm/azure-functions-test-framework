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
    private readonly IReadOnlyDictionary<string, string> _routeToFunctionMap;
    private readonly Dictionary<string, string> _exactRouteMap;
    private readonly List<RoutePatternEntry> _patternRoutes;
    private readonly string _routePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionsHttpMessageHandler"/> class.
    /// </summary>
    /// <param name="grpcHostService">The in-process gRPC host service used to dispatch invocations.</param>
    /// <param name="routeToFunctionMap">The loaded HTTP route map keyed by <c>{METHOD}:{route}</c>.</param>
    /// <param name="routePrefix">
    /// The HTTP route prefix configured in <c>host.json</c> (e.g. <c>"api"</c> or <c>"v1"</c>).
    /// Defaults to <c>"api"</c> when not specified.
    /// </param>
    public FunctionsHttpMessageHandler(
        GrpcHostService grpcHostService,
        IReadOnlyDictionary<string, string> routeToFunctionMap,
        string routePrefix = "api")
    {
        _grpcHostService = grpcHostService;
        _requestMapper = new HttpRequestMapper();
        _responseMapper = new HttpResponseMapper();
        _routeToFunctionMap = routeToFunctionMap;
        _routePrefix = routePrefix.Trim('/');
        _exactRouteMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _patternRoutes = new List<RoutePatternEntry>();

        foreach (var (routeKey, functionId) in _routeToFunctionMap)
        {
            var colonIdx = routeKey.IndexOf(':');
            if (colonIdx < 0)
            {
                continue;
            }

            var method = routeKey.Substring(0, colonIdx).ToUpperInvariant();
            // Route keys from the function map are bare routes (e.g. "todos/{id}") with no prefix.
            var route = NormalizePath(routeKey.Substring(colonIdx + 1), string.Empty);
            var routeSegments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var hasParameters = routeSegments.Any(static seg => seg.Length > 2 && seg[0] == '{' && seg[^1] == '}');

            if (!hasParameters)
            {
                _exactRouteMap[$"{method}:{route}"] = functionId;
                continue;
            }

            _patternRoutes.Add(new RoutePatternEntry(method, functionId, routeSegments));
        }
    }

    /// <summary>
    /// Sends an HTTP request through the in-process Functions worker and returns the mapped response.
    /// </summary>
    /// <param name="request">The HTTP request to send.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The HTTP response produced by the function invocation.</returns>
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

            // 3. Create gRPC InvocationRequest using the actual HTTP trigger binding parameter name
            //    from function metadata (e.g. "request" rather than the conventional "req").
            var httpBindingName = _grpcHostService.GetHttpTriggerBindingName(functionId);
            var grpcRequest = _requestMapper.CreateInvocationRequest(
                functionId: functionId,
                method: method,
                url: path,
                headers: headers,
                body: body,
                queryParams: queryParams,
                bindingName: httpBindingName
            );

            // 4. Add route parameters to InputData (binds direct function parameters like "string id", "Guid id")
            //    and to TriggerMetadata (populates FunctionContext.BindingContext.BindingData["id"]).
            foreach (var (paramName, paramValue) in routeParams)
            {
                // Write as RpcString; the worker SDK's TypedData converters unwrap to a string
                // which is then handled by type-specific converters (GuidConverter, etc.).
                grpcRequest.InvocationRequest.InputData.Add(new ParameterBinding
                {
                    Name = paramName,
                    Data = new TypedData { String = paramValue }
                });
                grpcRequest.InvocationRequest.TriggerMetadata[paramName] = new TypedData { String = paramValue };
            }

            foreach (var binding in _grpcHostService.GetSyntheticInputParameters(functionId))
            {
                grpcRequest.InvocationRequest.InputData.Add(GrpcHostService.ToParameterBinding(binding));
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
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        var queryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        var normalizedPath = NormalizePath(path, _routePrefix);
        var upperMethod = httpMethod.ToUpperInvariant();
        var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (_exactRouteMap.TryGetValue($"{upperMethod}:{normalizedPath}", out var exactFunctionId))
        {
            return (exactFunctionId, System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty);
        }

        foreach (var pattern in _patternRoutes)
        {
            if (!string.Equals(pattern.Method, upperMethod, StringComparison.Ordinal))
            {
                continue;
            }

            if (pattern.Segments.Length != pathSegments.Length)
            {
                continue;
            }

            var match = true;
            for (int i = 0; i < pattern.Segments.Length; i++)
            {
                var seg = pattern.Segments[i];
                if (seg.StartsWith('{') && seg.EndsWith('}'))
                {
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
                var routeParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < pattern.Segments.Length; i++)
                {
                    var seg = pattern.Segments[i];
                    if (seg.Length > 2 && seg[0] == '{' && seg[seg.Length - 1] == '}')
                    {
                        var paramName = seg.Substring(1, seg.Length - 2);
                        // Strip route constraint (e.g. "productId:guid" → "productId").
                        var colonIndex = paramName.IndexOf(':');
                        if (colonIndex > 0) paramName = paramName.Substring(0, colonIndex);
                        routeParams[paramName] = pathSegments[i];
                    }
                }
                return (pattern.FunctionId, routeParams);
            }
        }

        return (null, System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty);
    }

    private static string NormalizePath(string path, string routePrefix)
    {
        var normalizedPath = path.TrimStart('/');
        if (!string.IsNullOrEmpty(routePrefix))
        {
            var prefix = routePrefix.Trim('/') + "/";
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath.Substring(prefix.Length);
            }
        }

        var queryIndex = normalizedPath.IndexOf('?');
        if (queryIndex >= 0)
        {
            normalizedPath = normalizedPath.Substring(0, queryIndex);
        }

        return normalizedPath;
    }

    private sealed record RoutePatternEntry(string Method, string FunctionId, string[] Segments);

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
