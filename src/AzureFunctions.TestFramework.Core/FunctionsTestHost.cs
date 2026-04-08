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
public class FunctionsTestHost : IFunctionsTestHost, IHttpSupportedTestHost
{
    private readonly ILogger<FunctionsTestHost> _logger;
    private readonly GrpcServerManager _grpcServerManager;
    private readonly WorkerHostService _workerHostService;
    private readonly GrpcHostService _grpcHostService;
    private readonly string _routePrefix;
    private readonly TimeSpan _invocationTimeout;
    private bool _isStarted;

    private readonly IFunctionInvoker _invoker;

    internal FunctionsTestHost(
        ILogger<FunctionsTestHost> logger,
        GrpcServerManager grpcServerManager,
        WorkerHostService workerHostService,
        GrpcHostService grpcHostService,
        string routePrefix = "api",
        TimeSpan invocationTimeout = default)
    {
        _logger = logger;
        _grpcServerManager = grpcServerManager;
        _workerHostService = workerHostService;
        _grpcHostService = grpcHostService;
        _routePrefix = routePrefix.Trim('/');
        _invocationTimeout = invocationTimeout == default ? TimeSpan.FromSeconds(120) : invocationTimeout;
        _invoker = new FunctionInvoker(_grpcHostService);
    }

    /// <summary>
    /// Gets the worker service provider after the test host has started.
    /// </summary>
    public IServiceProvider Services => _workerHostService.Services;

    /// <summary>
    /// Gets the function invoker for executing functions.
    /// </summary>
    public IFunctionInvoker Invoker => _invoker;

    /// <inheritdoc/>
    public HttpMessageHandler? WorkerHttpHandler => _workerHostService.WorkerHttpHandler;

    /// <inheritdoc/>
    public GrpcHostService GrpcHostService => _grpcHostService;

    /// <inheritdoc/>
    public string RoutePrefix => _routePrefix;

    /// <inheritdoc/>
    public TimeSpan InvocationTimeout => _invocationTimeout;

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

    /// <summary>
    /// Asynchronously stops the host and releases all managed resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _workerHostService.DisposeAsync();
        await _grpcServerManager.DisposeAsync();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Stops the host and releases all managed resources.
    /// </summary>
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
/// Default function invoker that dispatches non-HTTP invocations via a registry of
/// <see cref="ITriggerBinding"/> implementations, one per trigger type.
/// </summary>
internal class FunctionInvoker : IFunctionInvoker
{
    private readonly GrpcHostService _grpcHostService;
    private readonly Dictionary<string, ITriggerBinding> _bindings
        = new(StringComparer.OrdinalIgnoreCase);

    public FunctionInvoker(GrpcHostService grpcHostService)
    {
        _grpcHostService = grpcHostService;
    }

    public void RegisterTriggerBinding(ITriggerBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _bindings.TryAdd(binding.TriggerType, binding);
    }

    public Task<FunctionInvocationResult> InvokeAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_bindings.TryGetValue(context.TriggerType, out var binding))
        {
            throw new NotSupportedException(
                $"Trigger type '{context.TriggerType}' is not supported by this invoker. " +
                $"Ensure the corresponding extension package is referenced and that its " +
                $"ITriggerBinding is registered via IFunctionInvoker.RegisterTriggerBinding.");
        }

        var registration = _grpcHostService.GetFunctionRegistration(functionName)
            ?? throw new InvalidOperationException(
                $"Function '{functionName}' was not found or is not a non-HTTP trigger function. " +
                $"Available non-HTTP functions: [{string.Join(", ", _grpcHostService.GetFunctions().Keys)}]");

        var bindingData = binding.CreateBindingData(context, registration);
        return _grpcHostService.InvokeFunctionAsync(functionName, bindingData, cancellationToken);
    }

    public IReadOnlyDictionary<string, IFunctionMetadata> GetFunctions()
    {
        return _grpcHostService.GetFunctions();
    }
}

