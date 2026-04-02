using AzureFunctions.TestFramework.Core.Grpc;
using AzureFunctions.TestFramework.Core.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Builder for creating FunctionsTestHost instances.
/// </summary>
public class FunctionsTestHostBuilder : IFunctionsTestHostBuilder
{
    private Assembly? _functionsAssembly;
    private readonly List<Action<IServiceCollection>> _serviceConfigurators = new();
    private readonly Dictionary<string, string> _settings = new();
    private readonly Dictionary<string, string> _environmentVariables = new();
    private Func<string[], IHostBuilder>? _hostBuilderFactory;
    private Func<string[], FunctionsApplicationBuilder>? _hostApplicationBuilderFactory;
    private ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Sets the function application assembly to load into the test host.
    /// </summary>
    /// <param name="assembly">The function application assembly.</param>
    /// <returns>The current builder.</returns>
    public IFunctionsTestHostBuilder WithFunctionAppAssembly(Assembly assembly)
    {
        _functionsAssembly = assembly;
        return this;
    }

    /// <summary>
    /// Adds a service-configuration callback that runs when the worker host is built.
    /// </summary>
    /// <param name="configure">The callback used to register or override services.</param>
    /// <returns>The current builder.</returns>
    public IFunctionsTestHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        _serviceConfigurators.Add(configure);
        return this;
    }

    /// <summary>
    /// Sets the functions assembly to load into the test host.
    /// </summary>
    /// <param name="assembly">The assembly containing the functions under test.</param>
    /// <returns>The current builder.</returns>
    public IFunctionsTestHostBuilder WithFunctionsAssembly(Assembly assembly)
    {
        _functionsAssembly = assembly;
        return this;
    }

    /// <summary>
    /// Sets the functions worker assembly to load into the test host.
    /// </summary>
    /// <param name="assembly">The assembly containing the worker functions.</param>
    /// <returns>The current builder.</returns>
    public IFunctionsTestHostBuilder WithFunctionsWorkerAssembly(Assembly assembly)
    {
        // For now, same as WithFunctionsAssembly
        return WithFunctionsAssembly(assembly);
    }

    /// <summary>
    /// Adds or overrides a configuration setting for the worker host.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    /// <returns>The current builder.</returns>
    public IFunctionsTestHostBuilder ConfigureSetting(string key, string value)
    {
        _settings[key] = value;
        return this;
    }

    /// <summary>
    /// Sets a process-level environment variable before the worker host starts.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The environment variable value.</param>
    /// <returns>The current builder.</returns>
    public IFunctionsTestHostBuilder ConfigureEnvironmentVariable(string name, string value)
    {
        _environmentVariables[name] = value;
        return this;
    }

    /// <inheritdoc/>
    public IFunctionsTestHostBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <inheritdoc/>
    public IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], IHostBuilder> factory)
    {
        _hostBuilderFactory = factory;
        return this;
    }

    /// <inheritdoc/>
    public IFunctionsTestHostBuilder WithHostApplicationBuilderFactory(
        Func<string[], FunctionsApplicationBuilder> factory)
    {
        _hostApplicationBuilderFactory = factory;
        return this;
    }

    /// <summary>
    /// Builds and starts the test host asynchronously.
    /// </summary>
    public async Task<IFunctionsTestHost> BuildAndStartAsync(CancellationToken cancellationToken = default)
    {
        var host = Build();
        await host.StartAsync(cancellationToken);
        return host;
    }

    /// <summary>
    /// Builds the test host without starting it.
    /// </summary>
    /// <returns>The constructed test host.</returns>
    public IFunctionsTestHost Build()
    {
        if (_functionsAssembly == null)
        {
            throw new InvalidOperationException(
                "Functions assembly must be specified. Use WithFunctionsAssembly() or CreateBuilder<TAssembly>()");
        }

        // Create logging — use the caller-supplied factory or fall back to console.
        var loggerFactory = _loggerFactory ?? LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        // Create gRPC components
        var grpcHostServiceLogger = loggerFactory.CreateLogger<GrpcHostService>();
        var grpcHostService = new GrpcHostService(grpcHostServiceLogger, _functionsAssembly);

        var grpcServerLogger = loggerFactory.CreateLogger<GrpcServerManager>();
        var grpcServerManager = new GrpcServerManager(grpcServerLogger, grpcHostService);

        // Start the gRPC server during Build() to get the actual port
        // This ensures WorkerHostService uses the correct port
        grpcServerManager.StartAsync().GetAwaiter().GetResult();
        var actualPort = grpcServerManager.Port;

        // Read route prefix early so WorkerHostService can propagate it to GrpcInvocationBridgeStartupFilter.
        // The filter calls SendInvocationRequestAsync and must strip the correct prefix from the
        // request path when matching routes (e.g. "storage" instead of the default "api").
        var routePrefix = ReadRoutePrefixFromHostJson(_functionsAssembly);

        // Create worker host service with the actual gRPC port
        var workerLogger = loggerFactory.CreateLogger<WorkerHostService>();
        var workerHostService = new WorkerHostService(
            workerLogger,
            actualPort,
            _functionsAssembly,
            grpcHostService,
            _hostBuilderFactory,
            _settings,
            _environmentVariables,
            routePrefix,
            _hostApplicationBuilderFactory);

        // Apply service configurators
        foreach (var configurator in _serviceConfigurators)
        {
            workerHostService.ConfigureServices(configurator);
        }

        // Create main test host
        var testHostLogger = loggerFactory.CreateLogger<FunctionsTestHost>();

        return new FunctionsTestHost(
            testHostLogger,
            grpcServerManager,
            workerHostService,
            grpcHostService,
            routePrefix);
    }

    private static string ReadRoutePrefixFromHostJson(Assembly functionsAssembly)
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

    private static int FindAvailablePort()
    {
        using var socket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Tcp);
        
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var port = ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
        return port;
    }
}
