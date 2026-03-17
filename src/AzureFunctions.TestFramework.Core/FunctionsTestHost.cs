using AzureFunctions.TestFramework.Core.Grpc;
using AzureFunctions.TestFramework.Core.Worker;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Main test host for Azure Functions integration tests.
/// Similar to WebApplicationFactory in ASP.NET Core.
/// </summary>
public class FunctionsTestHost : IFunctionsTestHost
{
    private readonly ILogger<FunctionsTestHost> _logger;
    private readonly GrpcServerManager _grpcServerManager;
    private readonly WorkerHostService _workerHostService;
    private readonly GrpcHostService _grpcHostService;
    private HttpMessageHandler? _cachedHandler;
    private bool _isStarted;

    internal FunctionsTestHost(
        ILogger<FunctionsTestHost> logger,
        GrpcServerManager grpcServerManager,
        WorkerHostService workerHostService,
        GrpcHostService grpcHostService)
    {
        _logger = logger;
        _grpcServerManager = grpcServerManager;
        _workerHostService = workerHostService;
        _grpcHostService = grpcHostService;
    }

    /// <summary>
    /// Gets the worker service provider after the test host has started.
    /// </summary>
    public IServiceProvider Services => _workerHostService.Services;

    /// <summary>
    /// Gets the function invoker for executing functions.
    /// </summary>
    public IFunctionInvoker Invoker => new FunctionInvoker(_grpcHostService);

    /// <summary>
    /// Creates an HttpClient configured to invoke functions in-process.
    /// Similar to WebApplicationFactory.CreateClient().
    /// When the worker uses <c>ConfigureFunctionsWebApplication()</c>, requests are forwarded
    /// to the worker's internal Kestrel HTTP server (ASP.NET Core integration mode).
    /// Otherwise requests are dispatched directly via the gRPC InvocationRequest channel.
    /// </summary>
    public HttpClient CreateHttpClient()
    {
        if (!_isStarted)
        {
            throw new InvalidOperationException("Test host must be started before creating HTTP client");
        }

        // ASP.NET Core integration mode: forward HTTP requests to the worker's Kestrel server.
        // The worker's startup filters handle x-ms-invocation-id injection and gRPC correlation.
        if (_workerHostService.HttpPort.HasValue)
        {
            var handler = _cachedHandler ??= new AspNetCoreForwardingHandler(_workerHostService.HttpPort.Value);
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://localhost/")
            };
        }

        // gRPC-direct mode (ConfigureFunctionsWorkerDefaults): dispatch via InvocationRequest.
        var grpcHandler = _cachedHandler ??= new Client.FunctionsHttpMessageHandler(
            _grpcHostService,
            _grpcHostService.FunctionRouteMap);
        return new HttpClient(grpcHandler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
    }

    /// <summary>
    /// Starts the test host: gRPC server, then Functions worker.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isStarted)
        {
            _logger.LogWarning("Test host is already started");
            return;
        }

        _logger.LogInformation("Starting Functions test host");

        try
        {
            // 1. Start Functions worker (connects to our gRPC server)
            var previousConnectionVersion = _grpcHostService.ConnectionVersion;
            await _workerHostService.StartAsync(cancellationToken);

            // 2. Wait for worker to connect to gRPC
            using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectionCts.CancelAfter(TimeSpan.FromSeconds(30));
            await _grpcHostService.WaitForConnectionAsync(previousConnectionVersion, connectionCts.Token);

            // 3. Wait for functions to be discovered and loaded
            using var functionLoadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            functionLoadCts.CancelAfter(TimeSpan.FromSeconds(60));
            await _grpcHostService.WaitForFunctionsLoadedAsync().WaitAsync(functionLoadCts.Token);

            _isStarted = true;
            _logger.LogInformation("Functions test host started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Functions test host");
            await StopAsync(cancellationToken);
            throw new FunctionsTestHostException("Failed to start test host", ex);
        }
    }

    /// <summary>
    /// Stops the test host: signals gRPC stream shutdown, then worker, then gRPC server.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isStarted)
        {
            return;
        }

        _logger.LogInformation("Stopping Functions test host");

        try
        {
            // 1. Signal the EventStream to end gracefully before stopping the server.
            //    This prevents connection-abort exceptions from the gRPC framework.
            await _grpcHostService.SignalShutdownAsync();

            // 2. Stop in reverse order
            await _workerHostService.StopAsync(cancellationToken);
            await _grpcServerManager.StopAsync(cancellationToken);

            _isStarted = false;
            _logger.LogInformation("Functions test host stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Functions test host");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cachedHandler?.Dispose();
        await _workerHostService.DisposeAsync();
        await _grpcServerManager.DisposeAsync();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates a new FunctionsTestHost builder.
    /// </summary>
    public static IFunctionsTestHostBuilder CreateBuilder()
    {
        return new FunctionsTestHostBuilder();
    }

    /// <summary>
    /// Creates a test host for a specific function app assembly.
    /// </summary>
    public static IFunctionsTestHostBuilder CreateBuilder<TFunctionsAssembly>() where TFunctionsAssembly : class
    {
        return new FunctionsTestHostBuilder()
            .WithFunctionsAssembly(typeof(TFunctionsAssembly).Assembly);
    }
}

/// <summary>
/// Simple function invoker implementation.
/// </summary>
internal class FunctionInvoker : IFunctionInvoker
{
    private readonly GrpcHostService _grpcHostService;

    public FunctionInvoker(GrpcHostService grpcHostService)
    {
        _grpcHostService = grpcHostService;
    }

    public Task<FunctionInvocationResult> InvokeAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        return context.TriggerType switch
        {
            "timerTrigger" => InvokeTimerAsync(functionName, context, cancellationToken),
            "serviceBusTrigger" => InvokeServiceBusAsync(functionName, context, cancellationToken),
            "queueTrigger" => InvokeQueueAsync(functionName, context, cancellationToken),
            "blobTrigger" => InvokeBlobAsync(functionName, context, cancellationToken),
            "eventGridTrigger" => InvokeEventGridAsync(functionName, context, cancellationToken),
            _ => throw new NotSupportedException(
                $"Trigger type '{context.TriggerType}' is not supported by this invoker. " +
                $"Use a trigger-specific extension package (e.g. AzureFunctions.TestFramework.Timer).")
        };
    }

    private Task<FunctionInvocationResult> InvokeTimerAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        var timerJson = context.InputData.TryGetValue("$timerJson", out var j)
            ? j?.ToString() ?? "{}"
            : "{}";
        return _grpcHostService.InvokeTimerFunctionAsync(functionName, timerJson, cancellationToken);
    }

    private Task<FunctionInvocationResult> InvokeServiceBusAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        var bodyBytes = context.InputData.TryGetValue("$messageBodyBytes", out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();
        var triggerMetadata = context.InputData.TryGetValue("$triggerMetadata", out var m)
            ? m?.ToString()
            : null;
        return _grpcHostService.InvokeServiceBusFunctionAsync(functionName, bodyBytes, triggerMetadata, cancellationToken);
    }

    private Task<FunctionInvocationResult> InvokeQueueAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        var messageBytes = context.InputData.TryGetValue("$queueMessageBytes", out var b) && b is byte[] bytes
            ? new ReadOnlyMemory<byte>(bytes)
            : ReadOnlyMemory<byte>.Empty;
        return _grpcHostService.InvokeQueueFunctionAsync(functionName, messageBytes, cancellationToken);
    }

    private Task<FunctionInvocationResult> InvokeBlobAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        var contentBytes = context.InputData.TryGetValue("$blobContentBytes", out var b) && b is byte[] bytes
            ? new ReadOnlyMemory<byte>(bytes)
            : ReadOnlyMemory<byte>.Empty;
        var triggerMetadata = context.InputData.TryGetValue("$triggerMetadata", out var m)
            ? m?.ToString()
            : null;
        return _grpcHostService.InvokeBlobFunctionAsync(functionName, contentBytes, triggerMetadata, cancellationToken);
    }

    private Task<FunctionInvocationResult> InvokeEventGridAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken)
    {
        var eventJson = context.InputData.TryGetValue("$eventJson", out var j)
            ? j?.ToString() ?? "{}"
            : "{}";
        return _grpcHostService.InvokeEventGridFunctionAsync(functionName, eventJson, cancellationToken);
    }

    public IReadOnlyDictionary<string, IFunctionMetadata> GetFunctions()
    {
        return _grpcHostService.GetFunctions();
    }
}


/// <summary>
/// An <see cref="HttpMessageHandler"/> that forwards requests to the worker's internal
/// ASP.NET Core HTTP server (used when the worker is started with
/// <c>ConfigureFunctionsWebApplication()</c>).
/// <para>
/// The handler rewrites the request URI to point to <c>http://127.0.0.1:{httpPort}</c> and
/// injects a synthetic <c>x-ms-invocation-id</c> header when absent.  The worker's
/// <c>InvocationIdStartupFilter</c> and <c>GrpcInvocationBridgeStartupFilter</c> then
/// correlate the request with a gRPC <c>InvocationRequest</c> so that
/// <c>WorkerRequestServicesMiddleware</c> can unblock and execute the function.
/// </para>
/// </summary>
internal sealed class AspNetCoreForwardingHandler : HttpMessageHandler
{
    private const string InvocationIdHeader = "x-ms-invocation-id";

    private readonly Uri _workerBaseUri;
    private readonly HttpMessageInvoker _inner;

    public AspNetCoreForwardingHandler(int httpPort)
    {
        _workerBaseUri = new Uri($"http://127.0.0.1:{httpPort}");
        _inner = new HttpMessageInvoker(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Rewrite URI to target the worker's Kestrel server while preserving path + query.
        var original = request.RequestUri!;
        request.RequestUri = new UriBuilder(_workerBaseUri)
        {
            Path = original.AbsolutePath,
            Query = original.Query.TrimStart('?')
        }.Uri;

        // Inject a synthetic invocation ID if the caller didn't provide one.
        if (!request.Headers.Contains(InvocationIdHeader))
        {
            request.Headers.TryAddWithoutValidation(InvocationIdHeader, Guid.NewGuid().ToString());
        }

        return await _inner.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
