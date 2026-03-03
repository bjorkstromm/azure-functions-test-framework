using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Implements the gRPC FunctionRpc service that the Functions worker connects to.
/// This simulates the Azure Functions host behavior.
/// </summary>
public class GrpcHostService : FunctionRpc.FunctionRpcBase
{
    private readonly ILogger<GrpcHostService> _logger;
    private readonly Assembly _functionsAssembly;
    private readonly Dictionary<string, TaskCompletionSource<StreamingMessage>> _pendingRequests = new();
    private readonly object _lock = new();
    private IServerStreamWriter<StreamingMessage>? _responseStream;
    private string _workerId = string.Empty;
    private TaskCompletionSource<StreamingMessage>? _workerInitTcs;
    private TaskCompletionSource<bool> _functionsLoadedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    // Key format: "{METHOD}:{route}", e.g. "GET:todos" or "POST:todos/{id}"
    private readonly Dictionary<string, string> _functionRouteToId = new(StringComparer.OrdinalIgnoreCase);

    public GrpcHostService(ILogger<GrpcHostService> logger, Assembly functionsAssembly)
    {
        _logger = logger;
        _functionsAssembly = functionsAssembly;
    }

    /// <summary>
    /// Gets a value indicating whether the worker has connected.
    /// </summary>
    public bool IsConnected => _responseStream != null;

    /// <summary>
    /// Gets the worker ID.
    /// </summary>
    public string WorkerId => _workerId;

    /// <summary>
    /// Gets a value indicating whether functions have been loaded.
    /// </summary>
    public bool IsFunctionsLoaded => _functionsLoadedTcs.Task.IsCompleted;

    /// <summary>
    /// Gets the route-to-functionId mapping for loaded functions.
    /// Key format is "{METHOD}:{route}", e.g. "GET:todos" or "POST:todos/{id}".
    /// </summary>
    public IReadOnlyDictionary<string, string> FunctionRouteMap => _functionRouteToId;

    /// <summary>
    /// Waits until all functions have been discovered and loaded.
    /// </summary>
    public Task WaitForFunctionsLoadedAsync() => _functionsLoadedTcs.Task;

    /// <summary>
    /// Handles the bidirectional streaming RPC between host and worker.
    /// </summary>
    public override async Task EventStream(
        IAsyncStreamReader<StreamingMessage> requestStream,
        IServerStreamWriter<StreamingMessage> responseStream,
        ServerCallContext context)
    {
        // Reset the loaded TCS so callers waiting on a new connection don't see stale state.
        _functionsLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _responseStream = responseStream;
        _logger.LogInformation("Worker connected to event stream");

        try
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                await HandleWorkerMessageAsync(message, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event stream");
            throw;
        }
        finally
        {
            _responseStream = null;
            _logger.LogInformation("Worker disconnected from event stream");
        }
    }

    /// <summary>
    /// Sends a message to the worker and waits for a response.
    /// </summary>
    public async Task<StreamingMessage> SendMessageAsync(
        StreamingMessage message,
        CancellationToken cancellationToken = default)
    {
        if (_responseStream == null)
        {
            throw new InvalidOperationException("Worker is not connected");
        }

        var requestId = message.RequestId;
        if (string.IsNullOrEmpty(requestId))
        {
            requestId = Guid.NewGuid().ToString();
            message.RequestId = requestId;
        }

        var tcs = new TaskCompletionSource<StreamingMessage>();
        
        lock (_lock)
        {
            _pendingRequests[requestId] = tcs;
        }

        try
        {
            await _responseStream.WriteAsync(message, cancellationToken);
            _logger.LogDebug("Sent message: {MessageType}, RequestId: {RequestId}", 
                GetMessageType(message), requestId);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            return await tcs.Task.WaitAsync(cts.Token);
        }
        finally
        {
            lock (_lock)
            {
                _pendingRequests.Remove(requestId);
            }
        }
    }

    /// <summary>
    /// Sends a message to the worker without waiting for a response.
    /// </summary>
    public async Task SendMessageOneWayAsync(
        StreamingMessage message,
        CancellationToken cancellationToken = default)
    {
        if (_responseStream == null)
        {
            throw new InvalidOperationException("Worker is not connected");
        }

        await _responseStream.WriteAsync(message, cancellationToken);
        _logger.LogDebug("Sent one-way message: {MessageType}", GetMessageType(message));
    }

    /// <summary>
    /// Sends an <see cref="InvocationRequest"/> to the worker for a given invocation ID and HTTP
    /// route.  The invocation ID must match the <c>x-ms-invocation-id</c> header already placed
    /// on the in-flight request so that the worker's <c>IHttpCoordinator</c> can correlate it.
    /// </summary>
    /// <param name="invocationId">The correlation ID; must match the request header value.</param>
    /// <param name="httpMethod">The HTTP method (e.g. "GET", "POST").</param>
    /// <param name="requestPath">The raw request path (e.g. "/api/todos/123").</param>
    /// <param name="routePrefix">The functions route prefix (default "api").</param>
    public async Task SendInvocationRequestAsync(
        string invocationId,
        string httpMethod,
        string requestPath,
        string routePrefix = "api")
    {
        var (functionId, routeParams) = FindFunctionMatch(httpMethod, requestPath, routePrefix);
        if (functionId == null)
        {
            _logger.LogWarning(
                "No function found for {Method} {Path}; InvocationRequest not sent", httpMethod, requestPath);
            return;
        }

        var invocationRequest = new InvocationRequest
        {
            InvocationId = invocationId,
            FunctionId = functionId,
            TraceContext = new RpcTraceContext
            {
                TraceParent = invocationId,
                TraceState = string.Empty
            }
        };

        // Add route parameter values to InputData so the worker can bind them to function
        // parameters (e.g. "string id" in GetTodo([HttpTrigger] HttpRequestData req, string id)).
        foreach (var (name, value) in routeParams)
        {
            invocationRequest.InputData.Add(new ParameterBinding
            {
                Name = name,
                Data = new TypedData { String = value }
            });
        }

        var message = new StreamingMessage
        {
            // Use invocationId as RequestId so the InvocationResponse can be matched.
            RequestId = invocationId,
            InvocationRequest = invocationRequest
        };

        await SendMessageOneWayAsync(message);
        _logger.LogDebug("Sent InvocationRequest for {InvocationId} -> function {FunctionId}",
            invocationId, functionId);
    }

    /// <summary>
    /// Matches an incoming HTTP method + request path against the loaded function route map and
    /// returns the function ID if a match is found, or <c>null</c> otherwise.
    /// </summary>
    internal string? FindFunctionId(string httpMethod, string requestPath, string routePrefix = "api")
        => FindFunctionMatch(httpMethod, requestPath, routePrefix).FunctionId;

    /// <summary>
    /// Matches an incoming HTTP method + request path against the loaded function route map.
    /// Returns the function ID and a dictionary of extracted route parameter values (e.g.
    /// <c>{"id": "abc123"}</c> for a route pattern <c>todos/{id}</c>).
    /// </summary>
    internal (string? FunctionId, IReadOnlyDictionary<string, string> RouteParams) FindFunctionMatch(
        string httpMethod, string requestPath, string routePrefix = "api")
    {
        static IReadOnlyDictionary<string, string> Empty() =>
            System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty;

        // Strip leading slash and the route prefix (e.g. "/api/" or "api/").
        var path = requestPath.TrimStart('/');
        if (!string.IsNullOrEmpty(routePrefix))
        {
            var prefix = routePrefix.Trim('/') + "/";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(prefix.Length);
            }
        }

        var method = httpMethod.ToUpperInvariant();

        // 1. Exact match (no route parameters).
        if (_functionRouteToId.TryGetValue($"{method}:{path}", out var exactId))
        {
            return (exactId, Empty());
        }

        // 2. Pattern match: route segments with {param} placeholders.
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var (routeKey, functionId) in _functionRouteToId)
        {
            var colon = routeKey.IndexOf(':');
            if (colon < 0) continue;

            if (!routeKey.AsSpan(0, colon).Equals(method, StringComparison.OrdinalIgnoreCase)) continue;

            var routePattern = routeKey.AsSpan(colon + 1);
            var routeSegments = routePattern.ToString()
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (routeSegments.Length != pathSegments.Length) continue;

            var match = true;
            for (var i = 0; i < routeSegments.Length; i++)
            {
                var seg = routeSegments[i];
                // A segment enclosed in braces is a parameter placeholder – matches anything.
                if (seg.Length > 2 && seg[0] == '{' && seg[seg.Length - 1] == '}') continue;
                if (!seg.Equals(pathSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                // Extract route parameter values (e.g. "{id}" → pathSegments[i]).
                var routeParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < routeSegments.Length; i++)
                {
                    var seg = routeSegments[i];
                    if (seg.Length > 2 && seg[0] == '{' && seg[seg.Length - 1] == '}')
                    {
                        var paramName = seg.Substring(1, seg.Length - 2);
                        routeParams[paramName] = pathSegments[i];
                    }
                }
                return (functionId, routeParams);
            }
        }

        return (null, Empty());
    }

    private async Task HandleWorkerMessageAsync(
        StreamingMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Received message: {MessageType}, RequestId: {RequestId}",
            GetMessageType(message), message.RequestId);

        switch (message.ContentCase)
        {
            case StreamingMessage.ContentOneofCase.StartStream:
                await HandleStartStreamAsync(message, cancellationToken);
                break;

            case StreamingMessage.ContentOneofCase.WorkerInitResponse:
                _workerInitTcs?.TrySetResult(message);
                break;

            case StreamingMessage.ContentOneofCase.FunctionLoadResponse:
            case StreamingMessage.ContentOneofCase.FunctionMetadataResponse:
                CompleteRequest(message);
                break;

            case StreamingMessage.ContentOneofCase.InvocationResponse:
                if (message.InvocationResponse?.Result?.Status != StatusResult.Types.Status.Success)
                {
                    var errMsg = message.InvocationResponse?.Result?.Exception?.Message ?? "unknown";
                    _logger.LogError("Invocation failed for {InvocationId}: {Error}",
                        message.InvocationResponse?.InvocationId, errMsg);
                }
                CompleteRequest(message);
                break;

            case StreamingMessage.ContentOneofCase.RpcLog:
                HandleRpcLog(message.RpcLog);
                break;

            default:
                _logger.LogWarning("Unhandled message type: {MessageType}", message.ContentCase);
                break;
        }
    }

    private async Task HandleStartStreamAsync(
        StreamingMessage message,
        CancellationToken cancellationToken)
    {
        _workerId = message.StartStream.WorkerId;
        _logger.LogInformation("Worker {WorkerId} started stream", _workerId);

        // Create TCS to track WorkerInitResponse
        _workerInitTcs = new TaskCompletionSource<StreamingMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        var initRequest = new StreamingMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            WorkerInitRequest = new WorkerInitRequest
            {
                HostVersion = "1.0.0",
                FunctionAppDirectory = Path.GetDirectoryName(_functionsAssembly.Location) ?? AppContext.BaseDirectory
            }
        };

        initRequest.WorkerInitRequest.Capabilities.Add("TypedDataCollection", "true");
        initRequest.WorkerInitRequest.Capabilities.Add("WorkerStatus", "true");

        await SendMessageOneWayAsync(initRequest, cancellationToken);

        // Load functions in a background task so the EventStream loop can continue processing responses.
        // The Task.Run ensures we're off the EventStream thread, allowing the foreach loop to
        // receive and complete the WorkerInitResponse and subsequent responses.
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait for WorkerInitResponse via the TCS set in HandleWorkerMessageAsync
                using var initCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                initCts.CancelAfter(TimeSpan.FromSeconds(30));
                await _workerInitTcs.Task.WaitAsync(initCts.Token);

                _logger.LogInformation("Worker init complete, requesting function metadata");

                // Request function metadata from the worker
                var metadataRequest = new StreamingMessage
                {
                    RequestId = Guid.NewGuid().ToString(),
                    FunctionsMetadataRequest = new FunctionsMetadataRequest()
                };

                var metadataResponse = await SendMessageAsync(metadataRequest, cancellationToken);
                var functions = metadataResponse.FunctionMetadataResponse.FunctionMetadataResults;
                _logger.LogInformation("Received {Count} function(s) from worker", functions.Count);

                // Send FunctionLoadRequest for each function and build the route map
                foreach (var functionMetadata in functions)
                {
                    var loadRequest = new StreamingMessage
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        FunctionLoadRequest = new FunctionLoadRequest
                        {
                            FunctionId = functionMetadata.FunctionId,
                            Metadata = functionMetadata
                        }
                    };

                    var loadResponse = await SendMessageAsync(loadRequest, cancellationToken);
                    var loadStatus = loadResponse.FunctionLoadResponse?.Result?.Status;
                    if (loadStatus == StatusResult.Types.Status.Success)
                    {
                        _logger.LogInformation("Loaded function: {FunctionName} (ID: {FunctionId})",
                            functionMetadata.Name, functionMetadata.FunctionId);
                    }
                    else
                    {
                        var errorMessage = loadResponse.FunctionLoadResponse?.Result?.Exception?.Message ?? "Unknown error";
                        _logger.LogError("Failed to load function {FunctionName}: {Error}", functionMetadata.Name, errorMessage);
                        continue;
                    }

                    // Extract HTTP route from raw bindings and build the method-aware route map
                    foreach (var rawBinding in functionMetadata.RawBindings)
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(rawBinding);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("type", out var typeProp) &&
                                typeProp.GetString()?.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase) == true &&
                                root.TryGetProperty("route", out var routeProp))
                            {
                                var route = routeProp.GetString();
                                if (string.IsNullOrEmpty(route))
                                {
                                    continue;
                                }

                                // Extract accepted HTTP methods; default to all if not specified
                                var methods = new List<string>();
                                if (root.TryGetProperty("methods", out var methodsProp) &&
                                    methodsProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var m in methodsProp.EnumerateArray())
                                    {
                                        var methodStr = m.GetString();
                                        if (!string.IsNullOrEmpty(methodStr))
                                        {
                                            methods.Add(methodStr.ToUpperInvariant());
                                        }
                                    }
                                }

                                if (methods.Count == 0)
                                {
                                    // No methods specified means all methods are accepted; use wildcard
                                    methods.AddRange(new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" });
                                }

                                foreach (var httpMethod in methods)
                                {
                                    var key = $"{httpMethod}:{route}";
                                    _functionRouteToId[key] = functionMetadata.FunctionId;
                                    _logger.LogDebug("Mapped '{Key}' to function ID '{FunctionId}'",
                                        key, functionMetadata.FunctionId);
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore malformed bindings
                        }
                    }
                }

                _functionsLoadedTcs.TrySetResult(true);
                _logger.LogInformation("All functions loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading functions");
                _functionsLoadedTcs.TrySetException(ex);
            }
        }, cancellationToken);
    }

    private void CompleteRequest(StreamingMessage message)
    {
        if (string.IsNullOrEmpty(message.RequestId))
        {
            _logger.LogWarning("Received response without request ID");
            return;
        }

        lock (_lock)
        {
            if (_pendingRequests.TryGetValue(message.RequestId, out var tcs))
            {
                tcs.TrySetResult(message);
            }
            else
            {
                _logger.LogWarning("Received response for unknown request: {RequestId}", message.RequestId);
            }
        }
    }

    private void HandleRpcLog(RpcLog log)
    {
        var logLevel = log.Level switch
        {
            RpcLog.Types.Level.Trace => LogLevel.Trace,
            RpcLog.Types.Level.Debug => LogLevel.Debug,
            RpcLog.Types.Level.Information => LogLevel.Information,
            RpcLog.Types.Level.Warning => LogLevel.Warning,
            RpcLog.Types.Level.Error => LogLevel.Error,
            RpcLog.Types.Level.Critical => LogLevel.Critical,
            _ => LogLevel.None
        };

        _logger.Log(logLevel, "[Worker] {Message}", log.Message);
    }

    private static string GetMessageType(StreamingMessage message)
    {
        return message.ContentCase.ToString();
    }
}
