using AzureFunctions.TestFramework.Core.Routing;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;
using Google.Protobuf;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Implements the gRPC FunctionRpc service that the Functions worker connects to.
/// This simulates the Azure Functions host behavior.
/// </summary>
public class GrpcHostService : FunctionRpc.FunctionRpcBase
{
    private readonly ILogger<GrpcHostService> _logger;
    private readonly Assembly _functionsAssembly;
    private TimeSpan _invocationTimeout = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets or sets the timeout applied to each gRPC function invocation.
    /// Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable the timeout (useful when debugging).
    /// Defaults to 120 seconds.
    /// </summary>
    public TimeSpan InvocationTimeout
    {
        get => _invocationTimeout;
        set => _invocationTimeout = value;
    }

    private readonly List<ISyntheticBindingProvider> _syntheticBindingProviders;
    private readonly Dictionary<string, TaskCompletionSource<StreamingMessage>> _pendingRequests = new();
    private readonly object _lock = new();
    private readonly object _connectionLock = new();
    private IServerStreamWriter<StreamingMessage>? _responseStream;
    private string _workerId = string.Empty;
    private TaskCompletionSource<StreamingMessage>? _workerInitTcs;
    private TaskCompletionSource<bool> _functionsLoadedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<int> _connectionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource _shutdownCts = new();
    private TaskCompletionSource _eventStreamFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _connectionVersion;
    // Key format: "{METHOD}:{route}", e.g. "GET:todos" or "POST:todos/{id}"
    private readonly Dictionary<string, string> _functionRouteToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly RouteMatcher _routeMatcher = new();
    // Key: function name (case-insensitive); Value: FunctionRegistration
    private readonly Dictionary<string, FunctionRegistration> _functionsByName
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function ID; Value: synthetic input parameters injected by ISyntheticBindingProvider.
    private readonly Dictionary<string, List<FunctionBindingData>> _syntheticInputByFunctionId
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function ID; Value: the HTTP trigger binding parameter name (e.g. "req", "request").
    private readonly Dictionary<string, string> _httpBindingNameByFunctionId
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function name (case-insensitive); Value: IFunctionMetadata
    private readonly Dictionary<string, IFunctionMetadata> _functionMetadataMap
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcHostService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for host/worker protocol events.</param>
    /// <param name="functionsAssembly">The assembly containing the functions under test.</param>
    /// <param name="syntheticBindingProviders">
    /// Optional collection of <see cref="ISyntheticBindingProvider"/> implementations that inject
    /// synthetic input bindings (e.g. a <c>durableClient</c> payload) into every invocation of
    /// functions that declare the corresponding binding attribute.
    /// </param>
    public GrpcHostService(
        ILogger<GrpcHostService> logger,
        Assembly functionsAssembly,
        IEnumerable<ISyntheticBindingProvider>? syntheticBindingProviders = null)
    {
        _logger = logger;
        _functionsAssembly = functionsAssembly;
        _syntheticBindingProviders = syntheticBindingProviders?.ToList() ?? [];
    }

    /// <summary>
    /// Gets a value indicating whether the worker has connected.
    /// </summary>
    public bool IsConnected => _responseStream != null;

    /// <summary>
    /// Gets the current EventStream connection version.
    /// Incremented whenever a new worker stream connects.
    /// </summary>
    public int ConnectionVersion
    {
        get
        {
            lock (_connectionLock)
            {
                return _connectionVersion;
            }
        }
    }

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
    /// Gets the <see cref="RouteMatcher"/> built from loaded HTTP trigger routes.
    /// Supports route constraints, optional parameters, and catch-all segments.
    /// </summary>
    public RouteMatcher RouteMatcher => _routeMatcher;

    /// <summary>
    /// Gets the metadata for all discovered functions, keyed by function name.
    /// </summary>
    public IReadOnlyDictionary<string, IFunctionMetadata> GetFunctions() => _functionMetadataMap;

    /// <summary>
    /// Returns the <see cref="FunctionRegistration"/> for a non-HTTP function by name,
    /// or <see langword="null"/> if the function is not found or is an HTTP trigger.
    /// </summary>
    public FunctionRegistration? GetFunctionRegistration(string functionName)
        => _functionsByName.TryGetValue(functionName, out var reg) ? reg : null;

    /// <summary>
    /// Returns any synthetic input parameters registered for the given function ID.
    /// </summary>
    public IReadOnlyList<FunctionBindingData> GetSyntheticInputParameters(string functionId)
    {
        if (_syntheticInputByFunctionId.TryGetValue(functionId, out var bindings))
        {
            return bindings;
        }

        return [];
    }

    /// <summary>
    /// Converts a <see cref="FunctionBindingData"/> value to a gRPC <c>ParameterBinding</c>.
    /// </summary>
    public static ParameterBinding ToParameterBinding(FunctionBindingData data)
        => new() { Name = data.Name, Data = ToTypedData(data) };

    /// <summary>
    /// Converts a <see cref="FunctionBindingData"/> value to a gRPC <c>TypedData</c>.
    /// </summary>
    public static TypedData ToTypedData(FunctionBindingData data)
    {
        if (data.Bytes != null)
            return new TypedData { Bytes = ByteString.CopyFrom(data.Bytes) };
        if (data.Json != null)
            return new TypedData { Json = data.Json };
        if (data.StringValue != null)
            return new TypedData { String = data.StringValue };
        if (data.ModelBindingData != null)
            return new TypedData { ModelBindingData = ToModelBindingData(data.ModelBindingData) };
        if (data.CollectionModelBindingData != null)
        {
            var collection = new CollectionModelBindingData();
            foreach (var item in data.CollectionModelBindingData)
                collection.ModelBindingData.Add(ToModelBindingData(item));
            return new TypedData { CollectionModelBindingData = collection };
        }
        return new TypedData();
    }

    private static ModelBindingData ToModelBindingData(ModelBindingDataValue value)
        => new()
        {
            Version = value.Version,
            Source = value.Source,
            ContentType = value.ContentType,
            Content = ByteString.CopyFrom(value.Content)
        };

    /// <summary>
    /// Waits until all functions have been discovered and loaded.
    /// </summary>
    public Task WaitForFunctionsLoadedAsync() => _functionsLoadedTcs.Task;

    /// <summary>
    /// Waits until a worker connection newer than <paramref name="previousConnectionVersion"/> is active.
    /// </summary>
    public async Task WaitForConnectionAsync(
        int previousConnectionVersion,
        CancellationToken cancellationToken = default)
    {
        Task<int> waitTask;
        lock (_connectionLock)
        {
            if (_connectionVersion > previousConnectionVersion)
            {
                return;
            }

            waitTask = _connectionTcs.Task;
        }

        await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Signals the EventStream to shut down gracefully.
    /// </summary>
    public void RequestShutdown()
    {
        _shutdownCts.Cancel();
    }

    /// <summary>
    /// Waits for the current EventStream to finish after shutdown has been requested.
    /// </summary>
    public Task WaitForShutdownAsync(TimeSpan timeout)
        => _eventStreamFinished.Task.WaitAsync(timeout);

    /// <summary>
    /// Signals the EventStream to shut down gracefully and waits for it to complete.
    /// Call this before stopping the gRPC server to avoid connection-abort errors.
    /// </summary>
    public async Task SignalShutdownAsync()
    {
        RequestShutdown();
        await _eventStreamFinished.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the <see cref="CancellationTokenSource"/> and <see cref="TaskCompletionSource"/>
    /// for the currently active EventStream.  Callers should capture these immediately after the
    /// worker has finished loading functions and store them for later use (e.g. to signal a derived
    /// factory's EventStream to end before that factory's host is disposed).
    /// </summary>
    public (CancellationTokenSource ShutdownCts, TaskCompletionSource EventStreamFinished)
        GetCurrentEventStreamState() => (_shutdownCts, _eventStreamFinished);

    /// <summary>
    /// Handles the bidirectional streaming RPC between host and worker.
    /// </summary>
    public override async Task EventStream(
        IAsyncStreamReader<StreamingMessage> requestStream,
        IServerStreamWriter<StreamingMessage> responseStream,
        ServerCallContext context)
    {
        // Save the existing stream so it can be restored when a secondary worker (e.g. from
        // WithWebHostBuilder) disconnects while the primary worker is still active.
        var previousResponseStream = _responseStream;

        // Reset per-connection state so a reconnecting worker starts fresh.
        _functionsLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _shutdownCts = new CancellationTokenSource();
        _eventStreamFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _responseStream = responseStream;
        lock (_connectionLock)
        {
            _connectionVersion++;
            _connectionTcs.TrySetResult(_connectionVersion);
            _connectionTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _logger.LogInformation("Worker connected to event stream");

        // Link our shutdown token with the server's request cancellation token so either
        // side can end the stream — whichever fires first.
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _shutdownCts.Token, context.CancellationToken);

        try
        {
            await foreach (var message in requestStream.ReadAllAsync(linkedCts.Token))
            {
                await HandleWorkerMessageAsync(message, linkedCts.Token);
            }
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            _logger.LogDebug("Event stream cancelled (graceful shutdown)");
        }
        catch (IOException) when (linkedCts.IsCancellationRequested)
        {
            _logger.LogDebug("Event stream connection aborted (graceful shutdown)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event stream");
            throw;
        }
        finally
        {
            // Restore the previous stream so a concurrent primary EventStream is not broken
            // when a secondary EventStream (e.g. from WithWebHostBuilder) ends first.
            if (ReferenceEquals(_responseStream, responseStream))
            {
                _responseStream = previousResponseStream;
            }
            _eventStreamFinished.TrySetResult();
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
            cts.CancelAfter(_invocationTimeout);

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
    /// Sends an <c>InvocationRequest</c> to the worker for a given invocation ID and HTTP
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

        // Add route parameter values to InputData (binds direct function parameters like "string id", "Guid id")
        // and to TriggerMetadata (populates FunctionContext.BindingContext.BindingData["id"]).
        // Values are written as RpcString; the worker SDK's TypedData converters unwrap the string
        // and pass it to type-specific converters (GuidConverter, etc.) which handle the final parse.
        foreach (var (name, value) in routeParams)
        {
            invocationRequest.InputData.Add(new ParameterBinding
            {
                Name = name,
                Data = new TypedData { String = value }
            });
            invocationRequest.TriggerMetadata[name] = new TypedData { String = value };
        }

        // The real Azure Functions host always includes a ParameterBinding for the HTTP trigger
        // binding (e.g. "req") with an empty RpcHttp payload. Without this entry the worker's
        // FunctionsHttpProxyingMiddleware.IsHttpTriggerFunction check (which inspects InputBindings)
        // may fail, causing IHttpCoordinator coordination to be skipped and HttpContext to be
        // omitted from FunctionContext.Items — leaving HttpRequest and FunctionContext null inside
        // the function body when using ConfigureFunctionsWebApplication().
        var httpBindingName = GetHttpTriggerBindingName(functionId);
        invocationRequest.InputData.Add(new ParameterBinding
        {
            Name = httpBindingName,
            Data = new TypedData { Http = new RpcHttp() }
        });

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
    /// Invokes a non-HTTP triggered function by name using the pre-built binding data.
    /// Returns a <see cref="FunctionInvocationResult"/> describing success or failure.
    /// </summary>
    /// <param name="functionName">The name of the function (case-insensitive).</param>
    /// <param name="bindingData">The input parameters and optional trigger metadata for the invocation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<FunctionInvocationResult> InvokeFunctionAsync(
        string functionName,
        TriggerBindingData bindingData,
        CancellationToken cancellationToken = default)
    {
        if (!_functionsByName.TryGetValue(functionName, out var registration))
        {
            var available = string.Join(", ", _functionsByName.Keys);
            throw new InvalidOperationException(
                $"No function '{functionName}' found. Available functions: [{available}]");
        }

        var invocationId = Guid.NewGuid().ToString();
        var invocationRequest = new InvocationRequest
        {
            InvocationId = invocationId,
            FunctionId = registration.FunctionId,
            TraceContext = new RpcTraceContext
            {
                TraceParent = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00",
                TraceState = string.Empty
            }
        };

        foreach (var param in bindingData.InputData)
        {
            invocationRequest.InputData.Add(ToParameterBinding(param));
        }

        if (bindingData.TriggerMetadataJson != null)
        {
            foreach (var (key, jsonValue) in bindingData.TriggerMetadataJson)
            {
                invocationRequest.TriggerMetadata[key] = new TypedData { Json = jsonValue };
            }
        }

        // Inject synthetic input bindings (e.g. durable client payload) registered by
        // ISyntheticBindingProvider implementations.
        foreach (var syntheticParam in GetSyntheticInputParameters(registration.FunctionId))
        {
            invocationRequest.InputData.Add(ToParameterBinding(syntheticParam));
        }

        var message = new StreamingMessage
        {
            RequestId = invocationId,
            InvocationRequest = invocationRequest
        };

        var response = await SendMessageAsync(message, cancellationToken);
        var invResponse = response.InvocationResponse;
        var success = invResponse?.Result?.Status == StatusResult.Types.Status.Success;

        _logger.LogDebug("Invocation {InvocationId} for '{FunctionName}' {Result}",
            invocationId, functionName, success ? "succeeded" : "failed");

        return CreateInvocationResult(invocationId, invResponse);
    }

    private static FunctionInvocationResult CreateInvocationResult(
        string invocationId,
        InvocationResponse? invocationResponse)
    {
        var success = invocationResponse?.Result?.Status == StatusResult.Types.Status.Success;
        var logs = invocationResponse?.Result?.Logs.Select(log => log.Message).ToList() ?? [];

        Dictionary<string, object?> outputData = new(StringComparer.OrdinalIgnoreCase);
        if (invocationResponse != null)
        {
            foreach (var output in invocationResponse.OutputData)
            {
                outputData[output.Name] = ConvertTypedData(output.Data);
            }
        }

        return new FunctionInvocationResult
        {
            InvocationId = invocationId,
            Success = success,
            Error = success ? null : invocationResponse?.Result?.Exception?.Message,
            ReturnValue = invocationResponse is null ? null : ConvertTypedData(invocationResponse.ReturnValue),
            OutputData = outputData,
            Logs = logs
        };
    }

    private static object? ConvertTypedData(TypedData? data)
    {
        if (data == null)
        {
            return null;
        }

        return data.DataCase switch
        {
            TypedData.DataOneofCase.None => null,
            TypedData.DataOneofCase.String => data.String,
            TypedData.DataOneofCase.Json => ParseJsonValue(data.Json),
            TypedData.DataOneofCase.Bytes => data.Bytes.ToByteArray(),
            TypedData.DataOneofCase.Stream => data.Stream.ToByteArray(),
            TypedData.DataOneofCase.Int => data.Int,
            TypedData.DataOneofCase.Double => data.Double,
            TypedData.DataOneofCase.CollectionString => data.CollectionString.String.ToArray(),
            TypedData.DataOneofCase.CollectionBytes => data.CollectionBytes.Bytes.Select(static b => b.ToByteArray()).ToArray(),
            TypedData.DataOneofCase.CollectionDouble => data.CollectionDouble.Double.ToArray(),
            TypedData.DataOneofCase.CollectionSint64 => data.CollectionSint64.Sint64.ToArray(),
            TypedData.DataOneofCase.ModelBindingData => ConvertModelBindingData(data.ModelBindingData),
            TypedData.DataOneofCase.CollectionModelBindingData => data.CollectionModelBindingData.ModelBindingData
                .Select(ConvertModelBindingData)
                .ToArray(),
            TypedData.DataOneofCase.Http => data.Http,
            _ => null
        };
    }

    private static object? ParseJsonValue(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static object ConvertModelBindingData(ModelBindingData data)
    {
        return new
        {
            data.Version,
            Source = data.Source,
            ContentType = data.ContentType,
            Content = data.Content.ToByteArray()
        };
    }

    /// <summary>
    /// Returns the function ID for an HTTP trigger function matched by HTTP method and request path.
    /// </summary>
    public string? FindFunctionId(string httpMethod, string requestPath, string routePrefix = "api")
        => FindFunctionMatch(httpMethod, requestPath, routePrefix).FunctionId;

    /// <summary>
    /// Returns the HTTP trigger binding parameter name (e.g. <c>"req"</c> or <c>"request"</c>)
    /// for the given function ID, as declared in the function's source-generated metadata.
    /// Falls back to <c>"req"</c> if the function is not known or has no httpTrigger binding.
    /// </summary>
    public string GetHttpTriggerBindingName(string functionId)
        => _httpBindingNameByFunctionId.TryGetValue(functionId, out var name) ? name : "req";

    /// <summary>
    /// Matches an incoming HTTP method + request path against the loaded function route map.
    /// Returns the function ID and a dictionary of extracted route parameter values (e.g.
    /// <c>{"id": "abc123"}</c> for a route pattern <c>todos/{id}</c>).
    /// Supports route constraints, optional parameters, and catch-all segments.
    /// </summary>
    public (string? FunctionId, IReadOnlyDictionary<string, string> RouteParams) FindFunctionMatch(
        string httpMethod, string requestPath, string routePrefix = "api")
    {
        // Strip leading slash and the route prefix (e.g. "/api/" or "api/").
        var path = requestPath.TrimStart('/');
        if (!string.IsNullOrEmpty(routePrefix))
        {
            var prefix = routePrefix.Trim('/') + "/";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(prefix.Length);
        }

        // Strip query string.
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path.Substring(0, queryIndex);

        return _routeMatcher.Match(httpMethod, path);
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

                    // Parse raw bindings to populate route maps and unified function registry
                    foreach (var rawBinding in functionMetadata.RawBindings)
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(rawBinding);
                            var root = doc.RootElement;
                            if (!root.TryGetProperty("type", out var typeProp)) continue;

                            var bindingType = typeProp.GetString() ?? string.Empty;

                            if (bindingType.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase))
                            {
                                // When Route is not explicitly set on [HttpTrigger], the source-generated
                                // binding JSON omits the "route" property or sets it to null.
                                // Azure Functions defaults the route to the function name in that case.
                                var route = root.TryGetProperty("route", out var routeProp)
                                    ? routeProp.GetString()
                                    : null;

                                if (string.IsNullOrEmpty(route))
                                {
                                    route = functionMetadata.Name;
                                }

                                // Store the actual HTTP trigger binding parameter name so callers can
                                // use it in InputData (instead of the hardcoded default "req").
                                var httpBindingName = root.TryGetProperty("name", out var httpNameProp)
                                    ? httpNameProp.GetString() ?? "req"
                                    : "req";
                                _httpBindingNameByFunctionId[functionMetadata.FunctionId] = httpBindingName;

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
                                    methods.AddRange(["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"]);
                                }

                                foreach (var httpMethod in methods)
                                {
                                    var key = $"{httpMethod}:{route}";
                                    _functionRouteToId[key] = functionMetadata.FunctionId;
                                    _routeMatcher.AddRoute(httpMethod, route, functionMetadata.FunctionId);
                                    _logger.LogDebug("Mapped '{Key}' to function ID '{FunctionId}'",
                                        key, functionMetadata.FunctionId);
                                }
                            }
                            else if (bindingType.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase))
                            {
                                // All non-HTTP triggers: populate the unified function registry.
                                // The "name" field is the binding parameter name in the function signature.
                                var paramName = root.TryGetProperty("name", out var nameProp)
                                    ? nameProp.GetString() ?? string.Empty
                                    : string.Empty;
                                _functionsByName[functionMetadata.Name] = new FunctionRegistration(
                                    functionMetadata.FunctionId,
                                    functionMetadata.Name,
                                    bindingType,
                                    paramName);
                                _logger.LogDebug(
                                    "Mapped {TriggerType} function '{Name}' (param '{Param}') to ID '{FunctionId}'",
                                    bindingType, functionMetadata.Name, paramName, functionMetadata.FunctionId);
                            }

                            // Allow registered ISyntheticBindingProvider implementations to inject
                            // synthetic input bindings for any binding type (e.g. durableClient).
                            foreach (var provider in _syntheticBindingProviders)
                            {
                                if (!bindingType.Equals(provider.BindingType, StringComparison.OrdinalIgnoreCase))
                                    continue;

                                var providerParamName = root.TryGetProperty("name", out var providerNameProp)
                                    ? providerNameProp.GetString() ?? provider.BindingType
                                    : provider.BindingType;

                                if (!_syntheticInputByFunctionId.TryGetValue(functionMetadata.FunctionId, out var bindings))
                                {
                                    bindings = [];
                                    _syntheticInputByFunctionId[functionMetadata.FunctionId] = bindings;
                                }

                                bindings.Add(provider.CreateSyntheticParameter(providerParamName, root));
                                _logger.LogDebug(
                                    "Added synthetic '{BindingType}' binding '{BindingName}' for function '{Name}'",
                                    provider.BindingType, providerParamName, functionMetadata.Name);
                            }
                        }
                        catch (JsonException)
                        {
                            // Ignore malformed bindings
                        }
                    }

                    // Store IFunctionMetadata for this function using DefaultFunctionMetadata
                    _functionMetadataMap[functionMetadata.Name] = new DefaultFunctionMetadata
                    {
                        Name = functionMetadata.Name,
                        EntryPoint = functionMetadata.EntryPoint,
                        ScriptFile = functionMetadata.ScriptFile,
                        RawBindings = functionMetadata.RawBindings.ToList()
                    };
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
                _logger.LogDebug("Received response for unknown request: {RequestId}", message.RequestId);
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

        var category = string.IsNullOrEmpty(log.Category) ? "Worker" : log.Category;
        _logger.Log(logLevel, "[{Category}] {Message}", category, log.Message);
    }

    private static string GetMessageType(StreamingMessage message)
    {
        return message.ContentCase.ToString();
    }
}
