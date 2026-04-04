using AzureFunctions.TestFramework.Core.Grpc;
using AzureFunctions.TestFramework.Core.Worker.Converters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
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
    // Serializes environment variable changes and host creation across parallel test instances
    // to avoid race conditions where concurrent hosts overwrite each other's env vars.
    private static readonly SemaphoreSlim s_envLock = new(1, 1);

    private readonly ILogger<WorkerHostService> _logger;
    private readonly int _grpcPort;
    private readonly Assembly _functionsAssembly;
    private readonly Func<string[], IHostBuilder>? _hostBuilderFactory;
    private readonly Func<string[], FunctionsApplicationBuilder>? _hostApplicationBuilderFactory;
    private readonly GrpcHostService _grpcHostService;
    private readonly IReadOnlyDictionary<string, string> _settings;
    private readonly IReadOnlyDictionary<string, string> _environmentVariables;
    private readonly string _routePrefix;
    private readonly List<Action<IServiceCollection>> _serviceConfigurators = new();
    private IHost? _workerHost;
    private bool _isInitialized;
    private int? _httpPort;
    private readonly int _allocatedHttpPort;
    private string? _tempAppDirectory;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerHostService"/> class.
    /// </summary>
    /// <param name="logger">The logger used for worker lifecycle messages.</param>
    /// <param name="grpcPort">The gRPC port exposed by the in-process host.</param>
    /// <param name="functionsAssembly">The assembly containing the functions under test.</param>
    /// <param name="grpcHostService">The gRPC host service coordinating worker communication.</param>
    /// <param name="hostBuilderFactory">Optional factory for creating the worker host builder (<see cref="IHostBuilder"/>).</param>
    /// <param name="settings">Optional configuration overrides for the worker host.</param>
    /// <param name="environmentVariables">Optional environment variables to set before startup.</param>
    /// <param name="routePrefix">The HTTP route prefix from host.json (default "api"). Forwarded to
    /// <see cref="GrpcInvocationBridgeStartupFilter"/> so it can match incoming paths correctly.</param>
    /// <param name="hostApplicationBuilderFactory">Optional factory for creating the worker host using
    /// <see cref="FunctionsApplicationBuilder"/> (<c>IHostApplicationBuilder</c> style).</param>
    public WorkerHostService(
        ILogger<WorkerHostService> logger,
        int grpcPort,
        Assembly functionsAssembly,
        GrpcHostService grpcHostService,
        Func<string[], IHostBuilder>? hostBuilderFactory = null,
        IReadOnlyDictionary<string, string>? settings = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        string routePrefix = "api",
        Func<string[], FunctionsApplicationBuilder>? hostApplicationBuilderFactory = null)
    {
        _logger = logger;
        _grpcPort = grpcPort;
        _functionsAssembly = functionsAssembly;
        _grpcHostService = grpcHostService;
        _hostBuilderFactory = hostBuilderFactory;
        _hostApplicationBuilderFactory = hostApplicationBuilderFactory;
        _settings = settings ?? new Dictionary<string, string>();
        _environmentVariables = environmentVariables ?? new Dictionary<string, string>();
        _routePrefix = routePrefix;
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
            // Create the worker host with Azure Functions infrastructure.
            // Environment variables (FUNCTIONS_APPLICATION_DIRECTORY, ASPNETCORE_URLS, etc.)
            // are process-global. To avoid races when tests run in parallel, serialize the
            // env-var-set → builder-create → host-build → host-start sequence so each host
            // reads its own values.  The lock must cover StartAsync because the SDK's
            // FunctionsEndpointDataSource reads FUNCTIONS_APPLICATION_DIRECTORY directly from
            // the environment at endpoint build time (during Kestrel startup).
            await s_envLock.WaitAsync(cancellationToken);
            try
            {
                _workerHost = CreateWorkerHost();
                await _workerHost.StartAsync(cancellationToken);
            }
            finally
            {
                s_envLock.Release();
            }

            // Auto-detect ASP.NET Core integration mode: if an IServer is registered in the
            // worker's DI container, the factory used ConfigureFunctionsWebApplication().
            // Read the ACTUAL port from Kestrel because ConfigureFunctionsWebApplication()
            // calls UseUrls(HttpUriProvider.HttpUriString) with its own random port, which
            // overrides ASPNETCORE_URLS and configuration-based URLs.
            var server = _workerHost.Services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            if (server == null && _hostApplicationBuilderFactory != null)
            {
                // FunctionsApplicationBuilder may register IServer in the root container under
                // a different mechanism. Fall back to discovering the address via the generic host's
                // IHostedService that manages the Kestrel lifetime.
                server = TryResolveServerFromGenericHost(_workerHost.Services);
            }

            if (server != null)
            {
                var addressFeature = server.Features
                    .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
                if (addressFeature?.Addresses is { Count: > 0 } addresses)
                {
                    _logger.LogWarning("DEBUG IServerAddressesFeature.Addresses: [{Addresses}], allocated port: {AllocatedPort}", string.Join(", ", addresses), _allocatedHttpPort);
                    var uri = new Uri(addresses.First());
                    _httpPort = uri.Port;
                }
                else
                {
                    _logger.LogWarning("DEBUG IServerAddressesFeature has no addresses; using allocated port {Port}", _allocatedHttpPort);
                    _httpPort = _allocatedHttpPort;
                }
                _logger.LogInformation(
                    "ASP.NET Core integration detected; worker HTTP server on port {HttpPort}", _httpPort);
            }
            else if (_hostApplicationBuilderFactory != null)
            {
                _logger.LogDebug("IServer not found in services after FunctionsApplicationBuilder start; running in gRPC mode");
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

    /// <summary>
    /// Asynchronously stops and disposes the worker host.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_workerHost != null)
        {
            await _workerHost.StopAsync();
            _workerHost.Dispose();
        }

        // Clean up temporary app directory created by ResolveFunctionAppDirectory.
        if (_tempAppDirectory != null)
        {
            try { Directory.Delete(_tempAppDirectory, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// When using <c>FunctionsApplicationBuilder</c>, the Kestrel <c>IServer</c> may be registered
    /// in a child container (e.g. inside the generic host's <c>IWebHostBuilder</c>-backed hosted
    /// service).  This helper attempts to find the server via the <c>IHostedService</c> collection
    /// so the ASP.NET Core port can be read back after the host has started.
    /// </summary>
    private static Microsoft.AspNetCore.Hosting.Server.IServer? TryResolveServerFromGenericHost(
        IServiceProvider services)
    {
        // WebApplication (used by FunctionsApplicationBuilder internally) exposes IServer directly.
        // Try the root container first; it's usually there for WebApplication/WebApplicationBuilder.
        var direct = services.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        if (direct != null) return direct;

        // Walk the IHostedService list — one of them is typically Microsoft.AspNetCore.Hosting.GenericWebHostService
        // which holds a reference to the inner IWebHost whose service provider has IServer.
        foreach (var hostedService in services.GetServices<IHostedService>())
        {
            // Avoid reflection failures on sealed types.
            try
            {
                var webHostProp = hostedService.GetType()
                    .GetProperty("Application", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (webHostProp?.GetValue(hostedService) is IServiceProvider innerSp)
                {
                    var innerServer = innerSp.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
                    if (innerServer != null) return innerServer;
                }

                var webHostField = hostedService.GetType()
                    .GetField("_webHost", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (webHostField?.GetValue(hostedService) is { } webHost)
                {
                    var servicesProp = webHost.GetType()
                        .GetProperty("Services", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                    if (servicesProp?.GetValue(webHost) is IServiceProvider webHostSp)
                    {
                        var innerServer = webHostSp.GetService<Microsoft.AspNetCore.Hosting.Server.IServer>();
                        if (innerServer != null) return innerServer;
                    }
                }
            }
            catch
            {
                // Ignore reflection failures — best effort.
            }
        }

        return null;
    }

    /// <summary>
    /// Diagnostic helper — logs key service registrations to help debug pipeline issues.
    /// </summary>
    private static void DiagnosticDumpServices(IServiceCollection services, ILogger logger)
    {
        // Check for IHttpCoordinator
        var httpCoordinatorCount = services.Count(d => d.ServiceType.FullName?.Contains("IHttpCoordinator") == true);
        logger.LogWarning("DIAG: IHttpCoordinator registrations: {Count}", httpCoordinatorCount);

        // Check for FunctionsHttpProxyingMiddleware
        var proxyMiddlewareCount = services.Count(d => 
            d.ServiceType.FullName?.Contains("FunctionsHttpProxyingMiddleware") == true ||
            d.ImplementationType?.FullName?.Contains("FunctionsHttpProxyingMiddleware") == true);
        logger.LogWarning("DIAG: FunctionsHttpProxyingMiddleware registrations: {Count}", proxyMiddlewareCount);

        // Check for FunctionExecutionDelegate
        var delegateCount = services.Count(d => d.ServiceType.FullName?.Contains("FunctionExecutionDelegate") == true);
        logger.LogWarning("DIAG: FunctionExecutionDelegate registrations: {Count}", delegateCount);

        // Check for IFunctionsWorkerApplicationBuilder
        var workerAppBuilderCount = services.Count(d => d.ServiceType.FullName?.Contains("IFunctionsWorkerApplicationBuilder") == true);
        logger.LogWarning("DIAG: IFunctionsWorkerApplicationBuilder registrations: {Count}", workerAppBuilderCount);

        // Check for FunctionsEndpointDataSource
        var endpointDataSourceCount = services.Count(d => 
            d.ServiceType.FullName?.Contains("FunctionsEndpointDataSource") == true ||
            d.ImplementationType?.FullName?.Contains("FunctionsEndpointDataSource") == true);
        logger.LogWarning("DIAG: FunctionsEndpointDataSource registrations: {Count}", endpointDataSourceCount);

        // Check for ExtensionTrace
        var extensionTraceCount = services.Count(d => d.ServiceType.FullName?.Contains("ExtensionTrace") == true);
        logger.LogWarning("DIAG: ExtensionTrace registrations: {Count}", extensionTraceCount);

        // Check for IServer
        var serverCount = services.Count(d => d.ServiceType.FullName == "Microsoft.AspNetCore.Hosting.Server.IServer");
        logger.LogWarning("DIAG: IServer registrations: {Count}", serverCount);

        // Check for GenericWebHostService
        var webHostServiceCount = services.Count(d => 
            d.ImplementationType?.FullName?.Contains("GenericWebHostService") == true);
        logger.LogWarning("DIAG: GenericWebHostService registrations: {Count}", webHostServiceCount);

        // Check for IStartupFilter
        var startupFilterCount = services.Count(d => d.ServiceType.FullName?.Contains("IStartupFilter") == true);
        logger.LogWarning("DIAG: IStartupFilter registrations: {Count}", startupFilterCount);
        foreach (var sf in services.Where(d => d.ServiceType.FullName?.Contains("IStartupFilter") == true))
        {
            var implName = sf.ImplementationType?.FullName ?? sf.ImplementationFactory?.Method?.ToString() ?? sf.ImplementationInstance?.GetType().FullName ?? "unknown";
            logger.LogWarning("DIAG:   IStartupFilter: {ImplName} (Lifetime={Lifetime})", implName, sf.Lifetime);
        }

        // Check for HttpContextConverter
        var httpContextConverterCount = services.Count(d => 
            d.ServiceType.FullName?.Contains("HttpContextConverter") == true ||
            d.ImplementationType?.FullName?.Contains("HttpContextConverter") == true);
        logger.LogWarning("DIAG: HttpContextConverter registrations: {Count}", httpContextConverterCount);

        // Dump WorkerOptions configure registrations
        var workerOptionsCount = services.Count(d => 
            d.ServiceType.FullName?.Contains("WorkerOptions") == true);
        logger.LogWarning("DIAG: WorkerOptions-related registrations: {Count}", workerOptionsCount);
    }

    private IHost CreateWorkerHost()
    {
        if (_hostApplicationBuilderFactory != null)
        {
            return CreateWorkerHostFromApplicationBuilder();
        }

        // Use 127.0.0.1 explicitly to avoid DNS resolution issues
        var grpcUri = $"http://127.0.0.1:{_grpcPort}";
        var workerId = Guid.NewGuid().ToString();
        var requestId = Guid.NewGuid().ToString();
        var httpUri = $"http://127.0.0.1:{_allocatedHttpPort}";
        
        _logger.LogInformation("Configuring worker to connect to {GrpcUri}", grpcUri);
        
        var functionAppDirectory = ResolveFunctionAppDirectory();
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
        var routePrefix = _routePrefix;
        var logger = _logger;
        hostBuilder.ConfigureServices(services =>
        {
            // Replace the SDK's DefaultMethodInfoLocator to prevent LoadFromAssemblyPath
            // from being called during FunctionLoadRequest processing.  This eliminates
            // the root cause of assembly dual-loading in in-process hosting.
            // Uses AddSingleton (not TryAdd) so it wins over the SDK's TryAddSingleton.
            InProcessMethodInfoLocator.TryRegister(services, logger);

            services.AddSingleton(grpcHostService);
            services.AddTransient<IStartupFilter, InvocationIdStartupFilter>();
            services.AddSingleton<IStartupFilter>(sp =>
                new GrpcInvocationBridgeStartupFilter(
                    sp.GetRequiredService<GrpcHostService>(),
                    sp.GetRequiredService<ILogger<GrpcInvocationBridgeStartupFilter>>(),
                    routePrefix));

            // In-process hosting can cause HttpContext type-identity mismatches between
            // the test runner's assembly load context and the worker's Kestrel pipeline.
            // These converters remain as defense-in-depth even with InProcessMethodInfoLocator:
            // if any other SDK code path loads assemblies from a different path, these
            // ensure FunctionContext and HttpRequest still bind correctly.
            // Set AFTF_SKIP_FALLBACK_CONVERTERS=1 to disable and rely solely on InProcessMethodInfoLocator.
            if (!string.Equals(Environment.GetEnvironmentVariable("AFTF_SKIP_FALLBACK_CONVERTERS"), "1", StringComparison.Ordinal))
            {
                services.PostConfigure<WorkerOptions>(options =>
                {
                    options.InputConverters.Register<TestHttpRequestConverter>();
                    options.InputConverters.Register<TestFunctionContextConverter>();
                });
            }
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

    /// <summary>
    /// Builds the worker <see cref="IHost"/> using the <see cref="FunctionsApplicationBuilder"/>
    /// (<c>IHostApplicationBuilder</c>) factory provided via
    /// <see cref="FunctionsTestHostBuilder.WithHostApplicationBuilderFactory"/>.
    /// </summary>
    private IHost CreateWorkerHostFromApplicationBuilder()
    {
        var grpcUri = $"http://127.0.0.1:{_grpcPort}";
        var workerId = Guid.NewGuid().ToString();
        var requestId = Guid.NewGuid().ToString();
        var httpUri = $"http://127.0.0.1:{_allocatedHttpPort}";

        var functionAppDirectory = ResolveFunctionAppDirectory();

        _logger.LogInformation("Configuring worker (HostApplicationBuilder) to connect to {GrpcUri}", grpcUri);

        // Set environment variables BEFORE calling the factory so they are picked up
        // by FunctionsApplication.CreateBuilder() which reads AddEnvironmentVariables() internally.
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME_VERSION", $"{Environment.Version.Major}.0");
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("FUNCTIONS_APPLICATION_DIRECTORY", functionAppDirectory);
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", httpUri);

        foreach (var (name, value) in _environmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
            _logger.LogDebug("Set environment variable '{Name}'", name);
        }

        // Call user factory — FunctionsApplication.CreateBuilder() sets up worker defaults,
        // registers middleware, and configures services.
        var appBuilder = _hostApplicationBuilderFactory!([]);

        // Add gRPC connection and runtime configuration after the factory so our values take
        // priority (IConfigurationManager evaluates sources in reverse order — last added wins).
        var allConfigValues = CreateConfigurationValues(
            grpcUri,
            workerId,
            requestId,
            functionAppDirectory,
            httpUri,
            includeFunctionDirectories: true,
            includeUrls: true);
        appBuilder.Configuration.AddInMemoryCollection(allConfigValues);

        // Add high-priority overrides (same pattern as IHostBuilder path).
        var overrideValues = CreateConfigurationValues(
            grpcUri,
            workerId,
            requestId,
            functionAppDirectory,
            httpUri,
            includeFunctionDirectories: false,
            includeUrls: false);
        appBuilder.Configuration.AddInMemoryCollection(overrideValues);

        // Apply user service configurators (test doubles may override production services).
        foreach (var configurator in _serviceConfigurators)
        {
            configurator(appBuilder.Services);
        }

        // Register test infrastructure services.
        var grpcHostService = _grpcHostService;
        var routePrefix = _routePrefix;
        var logger = _logger;

        InProcessMethodInfoLocator.TryRegister(appBuilder.Services, logger);

        appBuilder.Services.AddSingleton(grpcHostService);
        appBuilder.Services.AddTransient<IStartupFilter, InvocationIdStartupFilter>();
        appBuilder.Services.AddSingleton<IStartupFilter>(sp =>
            new GrpcInvocationBridgeStartupFilter(
                sp.GetRequiredService<GrpcHostService>(),
                sp.GetRequiredService<ILogger<GrpcInvocationBridgeStartupFilter>>(),
                routePrefix));

        if (!string.Equals(Environment.GetEnvironmentVariable("AFTF_SKIP_FALLBACK_CONVERTERS"), "1", StringComparison.Ordinal))
        {
            var diagLoggerForPostConfigure = _logger;
            appBuilder.Services.PostConfigure<WorkerOptions>(options =>
            {
                // Log and replace HttpContextConverter with a diagnostic wrapper
                var converterTypes = options.InputConverters.ToList();
                diagLoggerForPostConfigure.LogWarning("DIAG PostConfigure: InputConverters count={Count}", converterTypes.Count);
                
                // Register our TestHttpRequestConverter at position 0 to ensure it runs first
                options.InputConverters.RegisterAt<TestHttpRequestConverter>(0);
                options.InputConverters.Register<TestFunctionContextConverter>();
            });
        }

        // DIAGNOSTIC: Add inline middleware to check if FunctionsHttpProxyingMiddleware has run
        var diagLogger = logger;
        appBuilder.Use(next => async context =>
        {
            var hasHttpCtx = context.Items.TryGetValue("HttpRequestContext", out var httpCtxObj);
            
            // Log function parameter types from FunctionDefinition
            var paramInfo = string.Join(", ", context.FunctionDefinition?.Parameters.Select(
                p => $"{p.Name}:{p.Type.FullName}") ?? Enumerable.Empty<string>());
            diagLogger.LogWarning(
                "DIAG MIDDLEWARE: func={Func}, hasHttpCtx={HasHttpCtx}, httpCtxType={HType}, params=[{Params}]",
                context.FunctionDefinition?.Name ?? "??",
                hasHttpCtx,
                httpCtxObj?.GetType().FullName ?? "null",
                paramInfo);
            
            await next(context);
        });

        // Invoke IAutoConfigureStartup implementations using a shim that delegates
        // ConfigureServices calls to the application builder's service collection.
        // The source-generated FunctionMetadataProviderAutoStartup and FunctionExecutorAutoStartup
        // only call hostBuilder.ConfigureServices(...), so the shim is sufficient.
        var autoStartupAdapter = new HostBuilderToServiceCollectionAdapter(appBuilder.Services);
        foreach (var type in _functionsAssembly.GetTypes()
            .Where(t => typeof(IAutoConfigureStartup).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
        {
            try
            {
                var startup = (IAutoConfigureStartup)Activator.CreateInstance(type)!;
                startup.Configure(autoStartupAdapter);
                _logger.LogInformation("Registered auto-startup: {Type}", type.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register auto-startup: {Type}", type.FullName);
            }
        }

        // Configure logging to suppress noisy framework messages during tests,
        // but allow AzureFunctions.TestFramework.Core namespace at Information level
        // so startup filters and gRPC bridge activity is visible.
        appBuilder.Logging.SetMinimumLevel(LogLevel.Warning);
        appBuilder.Logging.AddFilter("Microsoft.Azure.Functions.Worker", LogLevel.Warning);
        appBuilder.Logging.AddFilter("Azure.Core", LogLevel.Warning);
        appBuilder.Logging.AddFilter("AzureFunctions.TestFramework", LogLevel.Information);

        // DIAGNOSTIC: Check what's registered in the service collection
        DiagnosticDumpServices(appBuilder.Services, logger);

        return appBuilder.Build();
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
    /// Returns the directory to use as <c>FUNCTIONS_APPLICATION_DIRECTORY</c>.
    /// When multiple function app assemblies share the same output directory, the generic
    /// <c>host.json</c> may belong to a different assembly.  If an assembly-specific
    /// <c>{AssemblyName}.host.json</c> exists, a temporary directory is created containing
    /// a <c>host.json</c> copied from the named file plus symlinks to all other content from
    /// the original directory, ensuring the SDK reads the correct route prefix while still
    /// finding all required assemblies and metadata.
    /// </summary>
    private string ResolveFunctionAppDirectory()
    {
        var assemblyDir = Path.GetDirectoryName(_functionsAssembly.Location) ?? AppContext.BaseDirectory;
        var assemblyName = _functionsAssembly.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName)) return assemblyDir;

        var namedHostJson = Path.Combine(assemblyDir, $"{assemblyName}.host.json");
        if (!File.Exists(namedHostJson)) return assemblyDir;

        // The named host.json exists — create a temp directory so the SDK's
        // GetRoutePrefixFromHostJson reads the correct file.
        var tempDir = Path.Combine(Path.GetTempPath(), $"aftf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _tempAppDirectory = tempDir;

        // Copy the named host.json as the generic host.json.
        File.Copy(namedHostJson, Path.Combine(tempDir, "host.json"), overwrite: true);

        // Symlink everything else from the original directory so the worker can find
        // assemblies, metadata, extensions, etc.
        foreach (var entry in Directory.EnumerateFileSystemEntries(assemblyDir))
        {
            var name = Path.GetFileName(entry);
            if (string.Equals(name, "host.json", StringComparison.OrdinalIgnoreCase)) continue;
            var target = Path.Combine(tempDir, name);
            if (!File.Exists(target) && !Directory.Exists(target))
            {
                try
                {
                    File.CreateSymbolicLink(target, entry);
                }
                catch
                {
                    // Symlinks may not be supported; fall back to copying the host.json approach.
                    // The worker should still find assemblies through AppDomain paths.
                }
            }
        }

        _logger.LogDebug("Created temporary app directory {TempDir} with host.json from {NamedHostJson}",
            tempDir, namedHostJson);

        return tempDir;
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
        private readonly string _routePrefix;

        public GrpcInvocationBridgeStartupFilter(
            GrpcHostService grpcHostService,
            ILogger<GrpcInvocationBridgeStartupFilter> logger,
            string routePrefix = "api")
        {
            _grpcHostService = grpcHostService;
            _logger = logger;
            _routePrefix = routePrefix;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                _logger.LogInformation("GrpcInvocationBridgeStartupFilter.Configure called — adding bridge middleware");
                var grpc = _grpcHostService;
                var logger = _logger;
                var routePrefix = _routePrefix;

                app.Use(async (context, nextMiddleware) =>
                {
                    if (!context.Request.Headers.TryGetValue(
                            InvocationIdHeader, out var invocationIdValues))
                    {
                        await nextMiddleware(context);
                        return;
                    }

                    var invocationId = invocationIdValues.ToString();
                    if (string.IsNullOrEmpty(invocationId))
                    {
                        await nextMiddleware(context);
                        return;
                    }

                    var method = context.Request.Method;
                    var path = context.Request.Path.Value ?? string.Empty;

                    // Send the InvocationRequest first and wait for it to be written to the
                    // gRPC stream. The worker picks it up (loopback TCP, sub-millisecond) and
                    // calls FunctionsHttpProxyingMiddleware.SetFunctionContextAsync, which
                    // unblocks WorkerRequestServicesMiddleware's 5-second wait for
                    // FunctionContextValueSource. Running the pipeline after ensures
                    // FunctionContextValueSource is already set when SetHttpContextAsync is
                    // called, so there is no race condition.
                    try
                    {
                        await grpc.SendInvocationRequestAsync(invocationId, method, path, routePrefix);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed to send InvocationRequest for {InvocationId} {Method} {Path}",
                            invocationId, method, path);
                    }

                    await nextMiddleware(context);
                });

                next(app);
            };
    }

    /// <summary>
    /// Minimal <see cref="IHostBuilder"/> adapter that delegates <c>ConfigureServices</c> calls
    /// to an existing <see cref="IServiceCollection"/>.  Used to invoke
    /// <see cref="IAutoConfigureStartup.Configure"/> implementations when the worker is built
    /// using a <see cref="FunctionsApplicationBuilder"/> (<c>IHostApplicationBuilder</c> style),
    /// which does not expose an <c>IHostBuilder</c> directly.
    /// </summary>
    private sealed class HostBuilderToServiceCollectionAdapter : IHostBuilder
    {
        private readonly IServiceCollection _services;

        public HostBuilderToServiceCollectionAdapter(IServiceCollection services)
            => _services = services;

        /// <inheritdoc />
        public IDictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        /// <inheritdoc />
        public IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate)
        {
            configureDelegate(null!, _services);
            return this;
        }

        /// <inheritdoc />
        public IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate) => this;

        /// <inheritdoc />
        public IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate) => this;

        /// <inheritdoc />
        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory)
            where TContainerBuilder : notnull => this;

        /// <inheritdoc />
        public IHostBuilder UseServiceProviderFactory<TContainerBuilder>(Func<HostBuilderContext, IServiceProviderFactory<TContainerBuilder>> factory)
            where TContainerBuilder : notnull => this;

        /// <inheritdoc />
        IHostBuilder IHostBuilder.ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate)
            => this;

        /// <inheritdoc />
        public IHost Build() =>
            throw new NotSupportedException("Use the FunctionsApplicationBuilder to build the host.");
    }
}
