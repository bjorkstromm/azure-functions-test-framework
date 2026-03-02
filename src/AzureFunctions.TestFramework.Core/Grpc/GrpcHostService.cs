using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;
using System.Reflection;

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
    /// Handles the bidirectional streaming RPC between host and worker.
    /// </summary>
    public override async Task EventStream(
        IAsyncStreamReader<StreamingMessage> requestStream,
        IServerStreamWriter<StreamingMessage> responseStream,
        ServerCallContext context)
    {
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
            case StreamingMessage.ContentOneofCase.FunctionLoadResponse:
            case StreamingMessage.ContentOneofCase.InvocationResponse:
            case StreamingMessage.ContentOneofCase.FunctionMetadataResponse:
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

        // Send WorkerInitRequest - but don't wait for response here!
        // The response will be handled in the message processing loop
        var initRequest = new StreamingMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            WorkerInitRequest = new WorkerInitRequest
            {
                HostVersion = "1.0.0",
                // Use the assembly's location as the function app directory
                // This is where the function metadata files should be
                FunctionAppDirectory = Path.GetDirectoryName(_functionsAssembly.Location) ?? AppContext.BaseDirectory
            }
        };

        initRequest.WorkerInitRequest.Capabilities.Add("RpcHttpBodyOnly", "true");
        initRequest.WorkerInitRequest.Capabilities.Add("RpcHttpTriggerMetadataRemoved", "true");
        initRequest.WorkerInitRequest.Capabilities.Add("TypedDataCollection", "true");
        initRequest.WorkerInitRequest.Capabilities.Add("WorkerStatus", "true");

        await SendMessageOneWayAsync(initRequest, cancellationToken);

        // Start async task to load functions after init completes
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait a bit for WorkerInitResponse
                await Task.Delay(1000);

                // Request function metadata from the worker
                var metadataRequest = new StreamingMessage
                {
                    RequestId = Guid.NewGuid().ToString(),
                    FunctionsMetadataRequest = new FunctionsMetadataRequest()
                };

                var metadataResponse = await SendMessageAsync(metadataRequest, cancellationToken);
                _logger.LogInformation("Received {Count} function(s) from worker",
                    metadataResponse.FunctionMetadataResponse.FunctionMetadataResults.Count);

                // Send FunctionLoadRequest for each function
                foreach (var functionMetadata in metadataResponse.FunctionMetadataResponse.FunctionMetadataResults)
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
                    _logger.LogInformation("Loaded function: {FunctionName} (ID: {FunctionId})",
                        functionMetadata.Name, functionMetadata.FunctionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading functions");
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
