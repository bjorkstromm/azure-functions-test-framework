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

    public GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger)
    {
        _logger = logger;
    }

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
