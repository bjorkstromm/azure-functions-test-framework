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
    // Key: function name (case-insensitive); Value: (FunctionId, BindingParameterName)
    private readonly Dictionary<string, (string FunctionId, string ParameterName)> _timerFunctionMap
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function name (case-insensitive); Value: (FunctionId, BindingParameterName)
    private readonly Dictionary<string, (string FunctionId, string ParameterName)> _serviceBusFunctionMap
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function name (case-insensitive); Value: (FunctionId, BindingParameterName)
    private readonly Dictionary<string, (string FunctionId, string ParameterName)> _queueFunctionMap
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function name (case-insensitive); Value: (FunctionId, BindingParameterName)
    private readonly Dictionary<string, (string FunctionId, string ParameterName)> _blobFunctionMap
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function name (case-insensitive); Value: (FunctionId, BindingParameterName)
    private readonly Dictionary<string, (string FunctionId, string ParameterName)> _eventGridFunctionMap
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function ID; Value: synthetic input bindings that should be added by the test host.
    private readonly Dictionary<string, List<ParameterBinding>> _syntheticInputBindingsByFunctionId
        = new(StringComparer.OrdinalIgnoreCase);
    // Key: function name (case-insensitive); Value: IFunctionMetadata
    private readonly Dictionary<string, IFunctionMetadata> _functionMetadataMap
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcHostService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for host/worker protocol events.</param>
    /// <param name="functionsAssembly">The assembly containing the functions under test.</param>
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
    /// Gets the metadata for all discovered functions, keyed by function name.
    /// </summary>
    public IReadOnlyDictionary<string, IFunctionMetadata> GetFunctions() => _functionMetadataMap;

    internal IReadOnlyList<ParameterBinding> GetSyntheticInputBindings(string functionId)
    {
        if (_syntheticInputBindingsByFunctionId.TryGetValue(functionId, out var bindings))
        {
            return bindings;
        }

        return [];
    }

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

        // Add route parameter values to InputData (binds direct function parameters like "string id")
        // and to TriggerMetadata (populates FunctionContext.BindingContext.BindingData["id"]).
        foreach (var (name, value) in routeParams)
        {
            invocationRequest.InputData.Add(new ParameterBinding
            {
                Name = name,
                Data = new TypedData { String = value }
            });
            invocationRequest.TriggerMetadata[name] = new TypedData { String = value };
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
    /// Invokes a timer-triggered function by name, passing <paramref name="timerInfoJson"/> as the
    /// timer input binding data.  Unlike <see cref="SendInvocationRequestAsync"/> (which is
    /// fire-and-forget), this method awaits the <see cref="InvocationResponse"/> and returns a
    /// <see cref="FunctionInvocationResult"/> describing success or failure.
    /// </summary>
    /// <param name="functionName">The name of the timer function (case-insensitive).</param>
    /// <param name="timerInfoJson">JSON-serialized timer info to pass as the trigger input.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<FunctionInvocationResult> InvokeTimerFunctionAsync(
        string functionName,
        string timerInfoJson,
        CancellationToken cancellationToken = default)
    {
        if (!_timerFunctionMap.TryGetValue(functionName, out var entry))
        {
            var available = string.Join(", ", _timerFunctionMap.Keys);
            throw new InvalidOperationException(
                $"No timer function '{functionName}' found. Available timer functions: [{available}]");
        }

        var invocationId = Guid.NewGuid().ToString();
        var invocationRequest = new InvocationRequest
        {
            InvocationId = invocationId,
            FunctionId = entry.FunctionId,
            TraceContext = new RpcTraceContext
            {
                TraceParent = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00",
                TraceState = string.Empty
            }
        };

        invocationRequest.InputData.Add(new ParameterBinding
        {
            Name = entry.ParameterName,
            Data = new TypedData { Json = timerInfoJson }
        });

        var message = new StreamingMessage
        {
            RequestId = invocationId,
            InvocationRequest = invocationRequest
        };

        var response = await SendMessageAsync(message, cancellationToken);
        var invResponse = response.InvocationResponse;
        var success = invResponse?.Result?.Status == StatusResult.Types.Status.Success;

        _logger.LogDebug("Timer invocation {InvocationId} for '{FunctionName}' {Result}",
            invocationId, functionName, success ? "succeeded" : "failed");

        return CreateInvocationResult(invocationId, invResponse);
    }

    /// <summary>
    /// Invokes a Service Bus–triggered function by name, passing <paramref name="bodyBytes"/> as the
    /// message body and optional <paramref name="triggerMetadataJson"/> as trigger metadata.
    /// Unlike <see cref="SendInvocationRequestAsync"/> this method awaits the
    /// <see cref="InvocationResponse"/> and returns a <see cref="FunctionInvocationResult"/>.
    /// </summary>
    /// <param name="functionName">The name of the Service Bus function (case-insensitive).</param>
    /// <param name="bodyBytes">The raw message body bytes to pass as the trigger input.</param>
    /// <param name="triggerMetadataJson">
    /// Optional JSON string containing message metadata (MessageId, ContentType, etc.)
    /// used by the worker to bind <c>ServiceBusReceivedMessage</c> parameters.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<FunctionInvocationResult> InvokeServiceBusFunctionAsync(
        string functionName,
        byte[] bodyBytes,
        string? triggerMetadataJson = null,
        CancellationToken cancellationToken = default)
    {
        if (!_serviceBusFunctionMap.TryGetValue(functionName, out var entry))
        {
            var available = string.Join(", ", _serviceBusFunctionMap.Keys);
            throw new InvalidOperationException(
                $"No Service Bus function '{functionName}' found. Available Service Bus functions: [{available}]");
        }

        var invocationId = Guid.NewGuid().ToString();
        var invocationRequest = new InvocationRequest
        {
            InvocationId = invocationId,
            FunctionId = entry.FunctionId,
            TraceContext = new RpcTraceContext
            {
                TraceParent = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00",
                TraceState = string.Empty
            }
        };

        invocationRequest.InputData.Add(new ParameterBinding
        {
            Name = entry.ParameterName,
            Data = new TypedData { Bytes = Google.Protobuf.ByteString.CopyFrom(bodyBytes) }
        });

        if (!string.IsNullOrEmpty(triggerMetadataJson))
        {
            invocationRequest.TriggerMetadata.Add(
                entry.ParameterName,
                new TypedData { Json = triggerMetadataJson });
        }

        var message = new StreamingMessage
        {
            RequestId = invocationId,
            InvocationRequest = invocationRequest
        };

        var response = await SendMessageAsync(message, cancellationToken);
        var invResponse = response.InvocationResponse;
        var success = invResponse?.Result?.Status == StatusResult.Types.Status.Success;

        _logger.LogDebug("Service Bus invocation {InvocationId} for '{FunctionName}' {Result}",
            invocationId, functionName, success ? "succeeded" : "failed");

        return CreateInvocationResult(invocationId, invResponse);
    }

    /// <summary>
    /// Invokes a queue-triggered function by name, passing <paramref name="messageBytes"/> as the
    /// queue message body.  Returns a <see cref="FunctionInvocationResult"/> describing success or failure.
    /// </summary>
    /// <param name="functionName">The name of the queue function (case-insensitive).</param>
    /// <param name="messageBytes">The raw bytes of the queue message body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<FunctionInvocationResult> InvokeQueueFunctionAsync(
        string functionName,
        ReadOnlyMemory<byte> messageBytes,
        CancellationToken cancellationToken = default)
    {
        if (!_queueFunctionMap.TryGetValue(functionName, out var entry))
        {
            var available = string.Join(", ", _queueFunctionMap.Keys);
            throw new InvalidOperationException(
                $"No queue function '{functionName}' found. Available queue functions: [{available}]");
        }

        var invocationId = Guid.NewGuid().ToString();
        var invocationRequest = new InvocationRequest
        {
            InvocationId = invocationId,
            FunctionId = entry.FunctionId,
            TraceContext = new RpcTraceContext
            {
                TraceParent = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00",
                TraceState = string.Empty
            }
        };

        invocationRequest.InputData.Add(new ParameterBinding
        {
            Name = entry.ParameterName,
            Data = new TypedData { Bytes = Google.Protobuf.ByteString.CopyFrom(messageBytes.Span) }
        });

        var message = new StreamingMessage
        {
            RequestId = invocationId,
            InvocationRequest = invocationRequest
        };

        var response = await SendMessageAsync(message, cancellationToken);
        var invResponse = response.InvocationResponse;
        var success = invResponse?.Result?.Status == StatusResult.Types.Status.Success;

        _logger.LogDebug("Queue invocation {InvocationId} for '{FunctionName}' {Result}",
            invocationId, functionName, success ? "succeeded" : "failed");

        return CreateInvocationResult(invocationId, invResponse);
    }

    /// <summary>
    /// Invokes a blob-triggered function by name, passing <paramref name="contentBytes"/> as the
    /// blob content and optional <paramref name="triggerMetadataJson"/> as trigger metadata.
    /// Returns a <see cref="FunctionInvocationResult"/> describing success or failure.
    /// </summary>
    /// <param name="functionName">The name of the blob function (case-insensitive).</param>
    /// <param name="contentBytes">The raw bytes of the blob content.</param>
    /// <param name="triggerMetadataJson">
    /// Optional JSON string containing blob metadata (BlobName, ContainerName, etc.)
    /// used by the worker to bind blob-related parameters.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<FunctionInvocationResult> InvokeBlobFunctionAsync(
        string functionName,
        ReadOnlyMemory<byte> contentBytes,
        string? triggerMetadataJson = null,
        CancellationToken cancellationToken = default)
    {
        if (!_blobFunctionMap.TryGetValue(functionName, out var entry))
        {
            var available = string.Join(", ", _blobFunctionMap.Keys);
            throw new InvalidOperationException(
                $"No blob function '{functionName}' found. Available blob functions: [{available}]");
        }

        var invocationId = Guid.NewGuid().ToString();
        var invocationRequest = new InvocationRequest
        {
            InvocationId = invocationId,
            FunctionId = entry.FunctionId,
            TraceContext = new RpcTraceContext
            {
                TraceParent = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00",
                TraceState = string.Empty
            }
        };

        invocationRequest.InputData.Add(new ParameterBinding
        {
            Name = entry.ParameterName,
            Data = new TypedData { Bytes = Google.Protobuf.ByteString.CopyFrom(contentBytes.Span) }
        });

        if (!string.IsNullOrEmpty(triggerMetadataJson))
        {
            invocationRequest.TriggerMetadata.Add(
                entry.ParameterName,
                new TypedData { Json = triggerMetadataJson });
        }

        var message = new StreamingMessage
        {
            RequestId = invocationId,
            InvocationRequest = invocationRequest
        };

        var response = await SendMessageAsync(message, cancellationToken);
        var invResponse = response.InvocationResponse;
        var success = invResponse?.Result?.Status == StatusResult.Types.Status.Success;

        _logger.LogDebug("Blob invocation {InvocationId} for '{FunctionName}' {Result}",
            invocationId, functionName, success ? "succeeded" : "failed");

        return CreateInvocationResult(invocationId, invResponse);
    }

    /// <summary>
    /// Invokes an Event Grid–triggered function by name, passing <paramref name="eventJson"/> as the
    /// serialized event data.  Returns a <see cref="FunctionInvocationResult"/> describing success or failure.
    /// </summary>
    /// <param name="functionName">The name of the Event Grid function (case-insensitive).</param>
    /// <param name="eventJson">JSON-serialized event (EventGridEvent or CloudEvent schema).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<FunctionInvocationResult> InvokeEventGridFunctionAsync(
        string functionName,
        string eventJson,
        CancellationToken cancellationToken = default)
    {
        if (!_eventGridFunctionMap.TryGetValue(functionName, out var entry))
        {
            var available = string.Join(", ", _eventGridFunctionMap.Keys);
            throw new InvalidOperationException(
                $"No Event Grid function '{functionName}' found. Available Event Grid functions: [{available}]");
        }

        var invocationId = Guid.NewGuid().ToString();
        var invocationRequest = new InvocationRequest
        {
            InvocationId = invocationId,
            FunctionId = entry.FunctionId,
            TraceContext = new RpcTraceContext
            {
                TraceParent = $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-00",
                TraceState = string.Empty
            }
        };

        invocationRequest.InputData.Add(new ParameterBinding
        {
            Name = entry.ParameterName,
            Data = new TypedData { Json = eventJson }
        });

        var message = new StreamingMessage
        {
            RequestId = invocationId,
            InvocationRequest = invocationRequest
        };

        var response = await SendMessageAsync(message, cancellationToken);
        var invResponse = response.InvocationResponse;
        var success = invResponse?.Result?.Status == StatusResult.Types.Status.Success;

        _logger.LogDebug("Event Grid invocation {InvocationId} for '{FunctionName}' {Result}",
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
                            if (root.TryGetProperty("type", out var typeProp))
                            {
                                var triggerType = typeProp.GetString() ?? string.Empty;

                                if (triggerType.Equals("httpTrigger", StringComparison.OrdinalIgnoreCase) &&
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
                                else if (triggerType.Equals("timerTrigger", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The "name" field is the function parameter name for the timer binding.
                                    var paramName = root.TryGetProperty("name", out var nameProp)
                                        ? nameProp.GetString() ?? "myTimer"
                                        : "myTimer";
                                    _timerFunctionMap[functionMetadata.Name] = (functionMetadata.FunctionId, paramName);
                                    _logger.LogDebug("Mapped timer function '{Name}' (param '{Param}') to ID '{FunctionId}'",
                                        functionMetadata.Name, paramName, functionMetadata.FunctionId);
                                }
                                else if (triggerType.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The "name" field is the function parameter name for the service bus binding.
                                    var paramName = root.TryGetProperty("name", out var nameProp)
                                        ? nameProp.GetString() ?? "message"
                                        : "message";
                                    _serviceBusFunctionMap[functionMetadata.Name] = (functionMetadata.FunctionId, paramName);
                                    _logger.LogDebug("Mapped Service Bus function '{Name}' (param '{Param}') to ID '{FunctionId}'",
                                        functionMetadata.Name, paramName, functionMetadata.FunctionId);
                                }
                                else if (triggerType.Equals("queueTrigger", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The "name" field is the function parameter name for the queue binding.
                                    var paramName = root.TryGetProperty("name", out var nameProp)
                                        ? nameProp.GetString() ?? "myQueueItem"
                                        : "myQueueItem";
                                    _queueFunctionMap[functionMetadata.Name] = (functionMetadata.FunctionId, paramName);
                                    _logger.LogDebug("Mapped queue function '{Name}' (param '{Param}') to ID '{FunctionId}'",
                                        functionMetadata.Name, paramName, functionMetadata.FunctionId);
                                }
                                else if (triggerType.Equals("blobTrigger", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The "name" field is the function parameter name for the blob binding.
                                    var paramName = root.TryGetProperty("name", out var nameProp)
                                        ? nameProp.GetString() ?? "myBlob"
                                        : "myBlob";
                                    _blobFunctionMap[functionMetadata.Name] = (functionMetadata.FunctionId, paramName);
                                    _logger.LogDebug("Mapped blob function '{Name}' (param '{Param}') to ID '{FunctionId}'",
                                        functionMetadata.Name, paramName, functionMetadata.FunctionId);
                                }
                                else if (triggerType.Equals("eventGridTrigger", StringComparison.OrdinalIgnoreCase))
                                {
                                    // The "name" field is the function parameter name for the Event Grid binding.
                                    var paramName = root.TryGetProperty("name", out var nameProp)
                                        ? nameProp.GetString() ?? "eventGridEvent"
                                        : "eventGridEvent";
                                    _eventGridFunctionMap[functionMetadata.Name] = (functionMetadata.FunctionId, paramName);
                                    _logger.LogDebug("Mapped Event Grid function '{Name}' (param '{Param}') to ID '{FunctionId}'",
                                        functionMetadata.Name, paramName, functionMetadata.FunctionId);
                                }
                                else if (triggerType.Equals("durableClient", StringComparison.OrdinalIgnoreCase))
                                {
                                    var paramName = root.TryGetProperty("name", out var nameProp)
                                        ? nameProp.GetString() ?? "durableClient"
                                        : "durableClient";
                                    var taskHub = root.TryGetProperty("taskHub", out var taskHubProp)
                                        ? taskHubProp.GetString()
                                        : null;
                                    var connectionName = root.TryGetProperty("connectionName", out var connectionNameProp)
                                        ? connectionNameProp.GetString()
                                        : null;

                                    if (!_syntheticInputBindingsByFunctionId.TryGetValue(functionMetadata.FunctionId, out var bindings))
                                    {
                                        bindings = [];
                                        _syntheticInputBindingsByFunctionId[functionMetadata.FunctionId] = bindings;
                                    }

                                    bindings.Add(new ParameterBinding
                                    {
                                        Name = paramName,
                                        Data = new TypedData
                                        {
                                            String = DurableClientBindingDefaults.CreatePayload(taskHub, connectionName)
                                        }
                                    });

                                    _logger.LogDebug(
                                        "Mapped durable client binding '{BindingName}' for function '{Name}' to synthetic payload",
                                        paramName,
                                        functionMetadata.Name);
                                }
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

        _logger.Log(logLevel, "[Worker] {Message}", log.Message);
    }

    private static string GetMessageType(StreamingMessage message)
    {
        return message.ContentCase.ToString();
    }
}
