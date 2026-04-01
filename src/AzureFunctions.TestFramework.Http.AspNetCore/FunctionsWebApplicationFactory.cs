using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Http.AspNetCore;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> for Azure Functions (dotnet-isolated) apps
/// that use <c>ConfigureFunctionsWebApplication()</c> (ASP.NET Core integration).
/// <para>
/// This factory starts an in-process gRPC host (to satisfy the Functions worker startup handshake),
/// configures the worker to connect to it, and exposes the app's ASP.NET Core pipeline via a
/// <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> — exactly like the standard
/// <see cref="WebApplicationFactory{TEntryPoint}"/> does for regular ASP.NET Core apps.
/// </para>
/// <para>
/// Because the full <c>Program.cs</c> is run, all services and middleware registered there are
/// available in tests. Use <see cref="WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/> or
/// override <see cref="ConfigureWebHost"/> to swap services for test doubles.
/// </para>
/// <para>
/// The entry-point class (<typeparamref name="TProgram"/>) must expose a
/// <c>public static IHostBuilder CreateHostBuilder(string[] args)</c> factory method so that
/// <see cref="WebApplicationFactory{TEntryPoint}"/> can intercept host creation.
/// Add <c>public partial class Program { }</c> to <c>Program.cs</c> to make the class visible.
/// </para>
/// </summary>
/// <typeparam name="TProgram">The entry-point class of the Azure Functions application.</typeparam>
public class FunctionsWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
    where TProgram : class
{
    // The header name that the Functions ASP.NET Core middleware uses to correlate HTTP requests
    // with invocation contexts. Normally added by the Azure Functions host.
    internal const string InvocationIdHeader = "x-ms-invocation-id";

    private readonly GrpcServerManager _grpcServerManager;
    private readonly GrpcHostService _grpcHostService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Action<IWebHostBuilder>? _additionalWebHostConfiguration;
    private readonly string _routePrefix;
    private readonly object _disposeLock = new();
    private bool _primaryHostCreated;
    private Task? _disposeTask;

    /// <summary>
    /// Initializes a new instance of <see cref="FunctionsWebApplicationFactory{TProgram}"/>
    /// and immediately starts the in-process gRPC host server.
    /// </summary>
    public FunctionsWebApplicationFactory()
        : this(null)
    {
    }

    private FunctionsWebApplicationFactory(Action<IWebHostBuilder>? additionalWebHostConfiguration)
    {
        _additionalWebHostConfiguration = additionalWebHostConfiguration;
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddConsole();
        });

        _grpcHostService = new GrpcHostService(
            _loggerFactory.CreateLogger<GrpcHostService>(),
            typeof(TProgram).Assembly);

        _grpcServerManager = new GrpcServerManager(
            _loggerFactory.CreateLogger<GrpcServerManager>(),
            _grpcHostService);

        _routePrefix = ReadRoutePrefixFromHostJson(typeof(TProgram).Assembly);

        // Start the gRPC server eagerly so the port is known before the host is built.
        _grpcServerManager.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the route-to-functionId map populated after the worker connects and loads functions.
    /// Key format is <c>"{METHOD}:{route}"</c>, e.g. <c>"GET:todos"</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> FunctionRouteMap => _grpcHostService.FunctionRouteMap;

    /// <summary>
    /// Creates a new independent factory with additional web-host configuration.
    /// </summary>
    public new FunctionsWebApplicationFactory<TProgram> WithWebHostBuilder(
        Action<IWebHostBuilder> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new FunctionsWebApplicationFactory<TProgram>(
            CombineWebHostConfiguration(_additionalWebHostConfiguration, configuration));
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var grpcHostService = _grpcHostService;
        var routePrefix = _routePrefix;

        builder.ConfigureServices(services =>
        {
            // Make the GrpcHostService available for injection into the bridge startup filter.
            services.AddSingleton(grpcHostService);
            services.Configure<HostOptions>(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromSeconds(1);
            });

            // InvocationIdStartupFilter must be registered first so its middleware runs first in
            // the pipeline, ensuring the x-ms-invocation-id header is present before the bridge
            // middleware reads it.
            services.AddTransient<IStartupFilter, InvocationIdStartupFilter>();

            // GrpcInvocationBridgeStartupFilter sends an InvocationRequest to the worker for
            // every incoming HTTP request.  The worker's IHttpCoordinator then unblocks the
            // WorkerRequestServicesMiddleware that is waiting for a matching FunctionContext.
            services.AddTransient<IStartupFilter>(_ => new GrpcInvocationBridgeStartupFilter(grpcHostService, routePrefix));
        });

        _additionalWebHostConfiguration?.Invoke(builder);
    }

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var previousConnectionVersion = _grpcHostService.ConnectionVersion;
        var grpcPort = _grpcServerManager.Port;
        var grpcUri = $"http://127.0.0.1:{grpcPort}";
        var workerId = Guid.NewGuid().ToString();
        var requestId = Guid.NewGuid().ToString();
        var functionAppDirectory = Path.GetDirectoryName(typeof(TProgram).Assembly.Location)
            ?? AppContext.BaseDirectory;

        // Set environment variables before any configuration is read.
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME_VERSION", "8.0");
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("FUNCTIONS_APPLICATION_DIRECTORY", functionAppDirectory);

        // Add at host-config level so it is visible to ConfigureFunctionsWorkerDefaults.
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Functions:Worker:HostEndpoint"] = grpcUri,
                ["Functions:Worker:WorkerId"] = workerId,
                ["Functions:Worker:RequestId"] = requestId,
                ["Functions:Worker:GrpcMaxMessageLength"] = "2147483647"
            });
        });

        // Add again at app-config level AFTER ConfigureFunctionsWorkerDefaults so that our
        // values win over any environment-variable overrides the Functions SDK may introduce.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Functions:Worker:HostEndpoint"] = grpcUri,
                ["Functions:Worker:WorkerId"] = workerId,
                ["Functions:Worker:RequestId"] = requestId,
                ["Functions:Worker:GrpcMaxMessageLength"] = "2147483647",
                ["AzureWebJobsScriptRoot"] = functionAppDirectory,
                ["FUNCTIONS_WORKER_DIRECTORY"] = functionAppDirectory
            });
        });

        // Invoke IAutoConfigureStartup implementations from the functions assembly.
        // This registers GeneratedFunctionMetadataProvider (IFunctionMetadataProvider) and
        // DirectFunctionExecutor (IFunctionExecutor), overriding the DefaultFunctionMetadataProvider
        // that would otherwise require a functions.metadata file to be present on disk.
        foreach (var type in typeof(TProgram).Assembly.GetTypes()
            .Where(t => typeof(IAutoConfigureStartup).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
        {
            try
            {
                var startup = (IAutoConfigureStartup)Activator.CreateInstance(type)!;
                startup.Configure(builder);
            }
            catch (Exception)
            {
                // Don't fail the factory if a startup type cannot be instantiated.
            }
        }

        var host = base.CreateHost(builder);

        // Block until the worker has connected and all functions are loaded so that
        // routes are registered in the ASP.NET Core router before any test request arrives.
        // Use Task.Run to ensure we execute on a thread-pool thread and avoid potential
        // synchronization-context deadlocks when GetAwaiter().GetResult() is called.
        Task.Run(() => WaitForFunctionsReadyAsync(previousConnectionVersion)).GetAwaiter().GetResult();

        if (_primaryHostCreated)
        {
            // This is a derived factory host (e.g., from WithWebHostBuilder).
            // GrpcWorker.StopAsync() is a no-op, so the worker's gRPC channel stays open
            // after the host's DI container is disposed.  Wrap the host so that we signal
            // the EventStream to end BEFORE the host is disposed, ensuring _responseStream
            // is restored to the primary worker's stream before the DI is torn down.
            var (shutdownCts, eventStreamFinished) = _grpcHostService.GetCurrentEventStreamState();
            return new GrpcAwareHost(host, shutdownCts, eventStreamFinished);
        }

        _primaryHostCreated = true;
        return host;
    }

    private async Task WaitForFunctionsReadyAsync(int previousConnectionVersion)
    {
        using var connectionCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _grpcHostService.WaitForConnectionAsync(previousConnectionVersion, connectionCts.Token);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await _grpcHostService.WaitForFunctionsLoadedAsync().WaitAsync(cts.Token);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        EnsureDisposedAsync(disposeAsync: false).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await EnsureDisposedAsync(disposeAsync: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private Task EnsureDisposedAsync(bool disposeAsync)
    {
        lock (_disposeLock)
        {
            _disposeTask ??= DisposeCoreAsync(disposeAsync);
            return _disposeTask;
        }
    }

    private async Task DisposeCoreAsync(bool disposeAsync)
    {
        if (disposeAsync)
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            // Call base FIRST to stop the TestServer and the Functions worker.
            base.Dispose(true);
        }

        // GrpcWorker.StopAsync() is a no-op, so the worker's gRPC channel can stay open
        // after the base factory disposal path completes. Request shutdown first, then
        // stop the gRPC server to actively break the read loop before awaiting stream exit.
        _grpcHostService.RequestShutdown();
        await _grpcServerManager.StopAsync().ConfigureAwait(false);
        await _grpcHostService.WaitForShutdownAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        await _grpcServerManager.DisposeAsync().ConfigureAwait(false);
        _loggerFactory.Dispose();
    }

    private static Action<IWebHostBuilder> CombineWebHostConfiguration(
        Action<IWebHostBuilder>? existing,
        Action<IWebHostBuilder> next)
    {
        if (existing is null)
        {
            return next;
        }

        return builder =>
        {
            existing(builder);
            next(builder);
        };
    }

    private static string ReadRoutePrefixFromHostJson(System.Reflection.Assembly functionsAssembly)
    {
        var assemblyDir = Path.GetDirectoryName(functionsAssembly.Location);
        if (assemblyDir == null) return "api";

        var hostJsonPath = Path.Combine(assemblyDir, "host.json");
        if (!File.Exists(hostJsonPath)) return "api";

        try
        {
            using var stream = File.OpenRead(hostJsonPath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("extensions", out var extensions) &&
                extensions.TryGetProperty("http", out var http) &&
                http.TryGetProperty("routePrefix", out var prefix))
            {
                return prefix.GetString() ?? "api";
            }
        }
        catch
        {
            // Ignore parse errors and fall back to the default.
        }

        return "api";
    }

    /// <summary>
    /// Startup filter that injects a synthetic <c>x-ms-invocation-id</c> header into every
    /// incoming request that does not already carry one.  The Azure Functions host normally
    /// provides this header; here we generate a new GUID so that the Functions ASP.NET Core
    /// middleware can correlate each test request with an invocation context.
    /// </summary>
    private sealed class InvocationIdStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(static async (context, nextMiddleware) =>
                {
                    if (!context.Request.Headers.ContainsKey(InvocationIdHeader))
                    {
                        context.Request.Headers[InvocationIdHeader] = Guid.NewGuid().ToString();
                    }

                    await nextMiddleware(context);
                });

                next(app);
            };
    }

    /// <summary>
    /// Startup filter that fires an <c>InvocationRequest</c> over the in-process gRPC
    /// channel for every incoming HTTP request.
    /// <para>
    /// In production the Azure Functions host creates an <c>InvocationRequest</c> and sends it
    /// to the isolated worker before forwarding the HTTP request to the worker's ASP.NET Core
    /// pipeline.  The worker's <c>WorkerRequestServicesMiddleware</c> blocks waiting for a
    /// matching <c>FunctionContext</c> supplied by the worker middleware (specifically
    /// <c>FunctionsHttpProxyingMiddleware</c>), which in turn is triggered by the
    /// <c>InvocationRequest</c>.  Without this bridge the request would hang indefinitely.
    /// </para>
    /// </summary>
    private sealed class GrpcInvocationBridgeStartupFilter : IStartupFilter
    {
        private readonly GrpcHostService _grpcHostService;
        private readonly string _routePrefix;

        public GrpcInvocationBridgeStartupFilter(GrpcHostService grpcHostService, string routePrefix)
        {
            _grpcHostService = grpcHostService;
            _routePrefix = routePrefix;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                var grpc = _grpcHostService;
                var routePrefix = _routePrefix;

                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue(
                            InvocationIdHeader, out var invocationIdValues))
                    {
                        var invocationId = invocationIdValues.ToString();
                        if (!string.IsNullOrEmpty(invocationId))
                        {
                            // Capture method and path before the fire-and-forget to avoid reading
                            // from a potentially recycled HttpContext after the request completes.
                            var method = context.Request.Method;
                            var path = context.Request.Path.Value ?? string.Empty;

                            // Fire-and-forget: send InvocationRequest so the worker's
                            // FunctionsHttpProxyingMiddleware calls SetFunctionContextAsync,
                            // which in turn unblocks WorkerRequestServicesMiddleware.
                            _ = Task.Run(() => grpc.SendInvocationRequestAsync(
                                invocationId,
                                method,
                                path,
                                routePrefix));
                        }
                    }

                    await nextMiddleware(context);
                });

                next(app);
            };
    }

    /// <summary>
    /// Wraps a derived factory's <see cref="IHost"/> to ensure the gRPC EventStream is
    /// signalled to end gracefully <em>before</em> the host's DI container is disposed.
    /// <para>
    /// <c>GrpcWorker.StopAsync</c> returns <c>Task.CompletedTask</c> immediately — it does not
    /// close the gRPC channel.  As a result, the secondary worker remains connected to our
    /// <see cref="GrpcHostService"/> EventStream after the host's DI container has been disposed.
    /// Any <c>InvocationRequest</c> that arrives during this window is processed by a worker whose
    /// <c>IServiceProvider</c> is already disposed, causing <see cref="ObjectDisposedException"/>.
    /// </para>
    /// <para>
    /// By cancelling the EventStream's <see cref="CancellationTokenSource"/> first, we force
    /// <see cref="GrpcHostService.EventStream"/> to exit its read loop, restore
    /// <c>_responseStream</c> to the primary worker's stream, and signal
    /// <c>_eventStreamFinished</c> — all before the host's DI is torn down.
    /// </para>
    /// </summary>
    private sealed class GrpcAwareHost : IHost
    {
        private readonly IHost _inner;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly TaskCompletionSource _eventStreamFinished;

        internal GrpcAwareHost(
            IHost inner,
            CancellationTokenSource shutdownCts,
            TaskCompletionSource eventStreamFinished)
        {
            _inner = inner;
            _shutdownCts = shutdownCts;
            _eventStreamFinished = eventStreamFinished;
        }

        public IServiceProvider Services => _inner.Services;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => _inner.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default)
            => _inner.StopAsync(cancellationToken);

        public void Dispose()
        {
            // Signal the EventStream to end and wait for _responseStream to be restored
            // before the inner host disposes its DI container.
            _shutdownCts.Cancel();
            _eventStreamFinished.Task
                .WaitAsync(TimeSpan.FromSeconds(5))
                .GetAwaiter()
                .GetResult();

            _inner.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            _shutdownCts.Cancel();
            await _eventStreamFinished.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            if (_inner is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                return;
            }

            _inner.Dispose();
        }
    }
}
