using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;

namespace AzureFunctions.TestFramework.Core.Worker;

/// <summary>
/// Manages the in-process Azure Functions Worker lifecycle.
/// Similar to WebApplicationFactory, this starts the Functions worker infrastructure
/// in the same process and connects it to our test gRPC server.
/// </summary>
public class WorkerHostService : IWorkerHost
{
    private readonly ILogger<WorkerHostService> _logger;
    private readonly int _grpcPort;
    private readonly Assembly _functionsAssembly;
    private readonly Func<string[], IHostBuilder>? _hostBuilderFactory;
    private readonly GrpcHostService _grpcHostService;
    private readonly IReadOnlyDictionary<string, string> _settings;
    private readonly IReadOnlyDictionary<string, string> _environmentVariables;
    private readonly List<Action<IServiceCollection>> _serviceConfigurators = new();
    private IHost? _workerHost;
    private bool _isInitialized;
    private int? _httpPort;
    private readonly int _allocatedHttpPort;

    /// <summary>
    /// The port on which the worker's ASP.NET Core HTTP server is listening, or <c>null</c>
    /// when the worker is using <c>ConfigureFunctionsWorkerDefaults()</c> (gRPC-direct mode).
    /// Populated after <see cref="StartAsync"/> when the worker has started successfully and
    /// an ASP.NET Core HTTP server (<c>IServer</c>) is detected in its service container.
    /// </summary>
    public int? HttpPort => _httpPort;

    /// <inheritdoc />
    public IServiceProvider Services => _workerHost?.Services
        ?? throw new InvalidOperationException("Worker host has not been started");

    public WorkerHostService(
        ILogger<WorkerHostService> logger,
        int grpcPort,
        Assembly functionsAssembly,
        GrpcHostService grpcHostService,
        Func<string[], IHostBuilder>? hostBuilderFactory = null,
        IReadOnlyDictionary<string, string>? settings = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        _logger = logger;
        _grpcPort = grpcPort;
        _functionsAssembly = functionsAssembly;
        _grpcHostService = grpcHostService;
        _hostBuilderFactory = hostBuilderFactory;
        _settings = settings ?? new Dictionary<string, string>();
        _environmentVariables = environmentVariables ?? new Dictionary<string, string>();
        _allocatedHttpPort = FindAvailablePort();
    }

    /// <summary>
    /// Gets a value indicating whether the worker is initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Adds a service configuration action to be executed when the worker starts.
    /// </summary>
    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        _serviceConfigurators.Add(configure);
    }

    /// <summary>
    /// Starts the Functions worker in-process.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            _logger.LogWarning("Worker is already initialized");
            return;
        }

        _logger.LogInformation("Starting in-process Functions worker");

        try
        {
            // Create the worker host with Azure Functions infrastructure
            _workerHost = CreateWorkerHost();

            // Start the worker
            await _workerHost.StartAsync(cancellationToken);

            // Auto-detect ASP.NET Core integration mode: if an IServer is registered in the
            // worker's DI container, the factory used ConfigureFunctionsWebApplication() and
            // the worker has a live HTTP server listening on the allocated HTTP port.
            if (_workerHost.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>() != null)
            {
                _httpPort = _allocatedHttpPort;
                _logger.LogInformation(
                    "ASP.NET Core integration detected; worker HTTP server on port {HttpPort}", _httpPort);
            }

            _isInitialized = true;
            _logger.LogInformation("In-process Functions worker started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start in-process Functions worker");
            throw new FunctionsTestHostException("Failed to start worker", ex);
        }
    }

    /// <summary>
    /// Stops the Functions worker.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_workerHost != null)
        {
            _logger.LogInformation("Stopping in-process Functions worker");
            await _workerHost.StopAsync(cancellationToken);
            _isInitialized = false;
            _logger.LogInformation("In-process Functions worker stopped");
        }
    }

    /// <summary>
    /// Not used in in-process mode - worker communicates via the gRPC server directly.
    /// </summary>
    public Task<WorkerMessage> SendMessageAsync(
        WorkerMessage message,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "Direct message sending is not supported in in-process mode. " +
            "The worker communicates via the gRPC server.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_workerHost != null)
        {
            await _workerHost.StopAsync();
            _workerHost.Dispose();
        }
    }

    private IHost CreateWorkerHost()
    {
        // Use 127.0.0.1 explicitly to avoid DNS resolution issues
        var grpcUri = $"http://127.0.0.1:{_grpcPort}";
        var workerId = Guid.NewGuid().ToString();
        var requestId = Guid.NewGuid().ToString();
        var httpUri = $"http://127.0.0.1:{_allocatedHttpPort}";
        
        _logger.LogInformation("Configuring worker to connect to {GrpcUri}", grpcUri);
        
        var functionAppDirectory = Path.GetDirectoryName(_functionsAssembly.Location) ?? AppContext.BaseDirectory;
        var hostConfigurationValues = CreateConfigurationValues(
            grpcUri,
            workerId,
            requestId,
            functionAppDirectory,
            httpUri,
            includeFunctionDirectories: false,
            includeUrls: true);
        var appConfigurationValues = CreateConfigurationValues(
            grpcUri,
            workerId,
            requestId,
            functionAppDirectory,
            httpUri,
            includeFunctionDirectories: true,
            includeUrls: false);
        var overrideConfigurationValues = CreateConfigurationValues(
            grpcUri,
            workerId,
            requestId,
            functionAppDirectory,
            httpUri,
            includeFunctionDirectories: false,
            includeUrls: false);

        // IMPORTANT: Set environment variables BEFORE creating HostBuilder!
        // ConfigureFunctionsWorkerDefaults calls AddEnvironmentVariables() which will read these
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME_VERSION", $"{Environment.Version.Major}.0");
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("FUNCTIONS_APPLICATION_DIRECTORY", functionAppDirectory);
        // Pre-configure the ASP.NET Core server URL so that when ConfigureFunctionsWebApplication()
        // is used the worker's Kestrel server listens on the allocated HTTP port.
        // Ignored when ConfigureFunctionsWorkerDefaults() is used (no Kestrel is started).
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", httpUri);

        // Apply user-configured environment variables (set BEFORE HostBuilder is created so they
        // are picked up by ConfigureFunctionsWorkerDefaults/AddEnvironmentVariables).
        foreach (var (name, value) in _environmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
            _logger.LogDebug("Set environment variable '{Name}'", name);
        }

        // Get the base HostBuilder: use the factory if provided, otherwise create a fresh one.
        // When a factory (e.g. Program.CreateWorkerHostBuilder) is supplied, all services,
        // middleware and configuration registered in Program.cs are included automatically.
        var hostBuilder = _hostBuilderFactory != null
            ? _hostBuilderFactory([])
            : new HostBuilder();

        // The worker's ConfigureFunctionsWorkerDefaults will:
        // 1. Add AZURE_FUNCTIONS_* environment variables via ConfigureHostConfiguration
        // 2. Add all environment variables via ConfigureAppConfiguration
        // 3. Add command-line args with switch mappings
        //
        // So we configure BEFORE calling ConfigureFunctionsWorkerDefaults
        hostBuilder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(hostConfigurationValues);
        });
        
        // Configure app configuration with the same values
        hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(appConfigurationValues);
        });

        if (_hostBuilderFactory == null)
        {
            // No factory provided: configure the Functions worker defaults here.
            // User service configurators are applied inside ConfigureFunctionsWorkerDefaults
            // so they are available to the worker's DI container.
            hostBuilder.ConfigureFunctionsWorkerDefaults((context, builder) =>
            {
                foreach (var configurator in _serviceConfigurators)
                {
                    configurator(builder.Services);
                }
            });
        }
        else
        {
            // Factory was provided: ConfigureFunctionsWorkerDefaults (or ConfigureFunctionsWebApplication)
            // was already called by the factory.  Apply user service configurators on top so that
            // test doubles can override services registered by the factory.
            if (_serviceConfigurators.Count > 0)
            {
                hostBuilder.ConfigureServices(services =>
                {
                    foreach (var configurator in _serviceConfigurators)
                    {
                        configurator(services);
                    }
                });
            }
        }
        
        // IMPORTANT: Add our configuration AFTER ConfigureFunctionsWorkerDefaults
        // so our values override any defaults from environment variables
        hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            // Add with high priority so they override environment variables
            config.AddInMemoryCollection(overrideConfigurationValues);
        });

        // Register the ASP.NET Core integration startup filters.
        // When ConfigureFunctionsWebApplication() is used, these filters are invoked by the
        // worker's ASP.NET Core pipeline for every incoming HTTP request:
        //   1. InvocationIdStartupFilter  – injects a synthetic x-ms-invocation-id header.
        //   2. GrpcInvocationBridgeStartupFilter – fires a gRPC InvocationRequest so that the
        //      worker's WorkerRequestServicesMiddleware can correlate the HTTP request with a
        //      FunctionContext and unblock.
        // When ConfigureFunctionsWorkerDefaults() is used, no IApplicationBuilder pipeline
        // exists so these filters are registered but never invoked — they are no-ops.
        var grpcHostService = _grpcHostService;
        hostBuilder.ConfigureServices(services =>
        {
            services.AddSingleton(grpcHostService);
            services.AddTransient<IStartupFilter, InvocationIdStartupFilter>();
            services.AddTransient<IStartupFilter, GrpcInvocationBridgeStartupFilter>();
        });

        // Discover and invoke IAutoConfigureStartup implementations from the functions assembly.
        // This registers the source-generated GeneratedFunctionMetadataProvider (IFunctionMetadataProvider)
        // and DirectFunctionExecutor (IFunctionExecutor) so the worker can discover and execute functions.
        foreach (var type in _functionsAssembly.GetTypes()
            .Where(t => typeof(IAutoConfigureStartup).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
        {
            try
            {
                var startup = (IAutoConfigureStartup)Activator.CreateInstance(type)!;
                startup.Configure(hostBuilder);
                _logger.LogInformation("Registered auto-startup: {Type}", type.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register auto-startup: {Type}", type.FullName);
            }
        }

        // Suppress unnecessary logging during tests
        hostBuilder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Microsoft.Azure.Functions.Worker", LogLevel.Warning);
            logging.AddFilter("Azure.Core", LogLevel.Warning);
        });

        return hostBuilder.Build();
    }

    private Dictionary<string, string?> CreateConfigurationValues(
        string grpcUri,
        string workerId,
        string requestId,
        string functionAppDirectory,
        string httpUri,
        bool includeFunctionDirectories,
        bool includeUrls)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Functions:Worker:HostEndpoint"] = grpcUri,
            ["Functions:Worker:WorkerId"] = workerId,
            ["Functions:Worker:RequestId"] = requestId,
            ["Functions:Worker:GrpcMaxMessageLength"] = "2147483647"
        };

        if (includeFunctionDirectories)
        {
            configValues["AzureWebJobsScriptRoot"] = functionAppDirectory;
            configValues["FUNCTIONS_WORKER_DIRECTORY"] = functionAppDirectory;
        }

        if (includeUrls)
        {
            configValues["urls"] = httpUri;
        }

        foreach (var setting in _settings)
        {
            configValues[setting.Key] = setting.Value;
        }

        return configValues;
    }

    private static int FindAvailablePort()
    {
        // Bind to port 0 so the OS assigns a free port, then read it back.
        // The socket is closed before the caller uses the port, leaving a small window for
        // another process to claim it.  This is an accepted trade-off in test utilities; the
        // probability of collision is very low in practice.
        using var socket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    /// <summary>
    /// Startup filter that injects a synthetic <c>x-ms-invocation-id</c> header into every
    /// incoming request that does not already carry one.  Used when the worker is configured
    /// with <c>ConfigureFunctionsWebApplication()</c> (ASP.NET Core integration mode).
    /// </summary>
    private sealed class InvocationIdStartupFilter : IStartupFilter
    {
        private const string InvocationIdHeader = "x-ms-invocation-id";

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
    /// Startup filter that fires an <see cref="Grpc.GrpcHostService.SendInvocationRequestAsync"/>
    /// call for every incoming HTTP request.  This unblocks the worker's
    /// <c>WorkerRequestServicesMiddleware</c> which waits for a matching <c>FunctionContext</c>
    /// created by the gRPC <c>InvocationRequest</c>.  Used when the worker is configured with
    /// <c>ConfigureFunctionsWebApplication()</c> (ASP.NET Core integration mode).
    /// </summary>
    private sealed class GrpcInvocationBridgeStartupFilter : IStartupFilter
    {
        private const string InvocationIdHeader = "x-ms-invocation-id";
        private readonly GrpcHostService _grpcHostService;
        private readonly ILogger<GrpcInvocationBridgeStartupFilter> _logger;

        public GrpcInvocationBridgeStartupFilter(
            GrpcHostService grpcHostService,
            ILogger<GrpcInvocationBridgeStartupFilter> logger)
        {
            _grpcHostService = grpcHostService;
            _logger = logger;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                var grpc = _grpcHostService;
                var logger = _logger;

                app.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Headers.TryGetValue(
                            InvocationIdHeader, out var invocationIdValues))
                    {
                        var invocationId = invocationIdValues.ToString();
                        if (!string.IsNullOrEmpty(invocationId))
                        {
                            var method = context.Request.Method;
                            var path = context.Request.Path.Value ?? string.Empty;

                            // Fire-and-forget: send InvocationRequest so the worker's
                            // FunctionsHttpProxyingMiddleware calls SetFunctionContextAsync,
                            // which in turn unblocks WorkerRequestServicesMiddleware.
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await grpc.SendInvocationRequestAsync(invocationId, method, path);
                                }
                                catch (Exception ex)
                                {
                                    logger.LogWarning(ex,
                                        "Failed to send InvocationRequest for {InvocationId} {Method} {Path}",
                                        invocationId, method, path);
                                }
                            });
                        }
                    }

                    await nextMiddleware(context);
                });

                next(app);
            };
    }
}
