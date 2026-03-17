using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Interceptor for logging gRPC calls.
/// </summary>
public class GrpcLoggingInterceptor : Interceptor
{
    private readonly ILogger<GrpcLoggingInterceptor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcLoggingInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger used to record gRPC call activity.</param>
    public GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles unary gRPC server calls and logs their lifecycle.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <param name="request">The incoming request message.</param>
    /// <param name="context">The server call context.</param>
    /// <param name="continuation">The next handler in the pipeline.</param>
    /// <returns>The response produced by the continuation.</returns>
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        _logger.LogDebug("gRPC unary call: {Method}", context.Method);
        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in gRPC call: {Method}", context.Method);
            throw;
        }
    }

    /// <summary>
    /// Handles duplex-streaming gRPC server calls and logs their lifecycle.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <param name="requestStream">The incoming request stream.</param>
    /// <param name="responseStream">The outgoing response stream.</param>
    /// <param name="context">The server call context.</param>
    /// <param name="continuation">The next handler in the pipeline.</param>
    /// <returns>A task that completes when the streaming call finishes.</returns>
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        _logger.LogDebug("gRPC streaming call started: {Method}", context.Method);
        try
        {
            await continuation(requestStream, responseStream, context);
            _logger.LogDebug("gRPC streaming call completed: {Method}", context.Method);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in gRPC streaming call: {Method}", context.Method);
            throw;
        }
    }
}
