using AzureFunctions.TestFramework.Core.Grpc;
using AzureFunctions.TestFramework.Core.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Builder for creating FunctionsTestHost instances.
/// </summary>
public class FunctionsTestHostBuilder : IFunctionsTestHostBuilder
{
    private Assembly? _functionsAssembly;
    private readonly List<Action<IServiceCollection>> _serviceConfigurators = new();
    private readonly Dictionary<string, string> _settings = new();
    private Func<string[], IHostBuilder>? _hostBuilderFactory;

    public IFunctionsTestHostBuilder WithFunctionAppAssembly(Assembly assembly)
    {
        _functionsAssembly = assembly;
        return this;
    }

    public IFunctionsTestHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        _serviceConfigurators.Add(configure);
        return this;
    }

    public IFunctionsTestHostBuilder WithFunctionsAssembly(Assembly assembly)
    {
        _functionsAssembly = assembly;
        return this;
    }

    public IFunctionsTestHostBuilder WithFunctionsWorkerAssembly(Assembly assembly)
    {
        // For now, same as WithFunctionsAssembly
        return WithFunctionsAssembly(assembly);
    }

    public IFunctionsTestHostBuilder ConfigureSetting(string key, string value)
    {
        _settings[key] = value;
        return this;
    }

    /// <inheritdoc/>
    public IFunctionsTestHostBuilder WithHostBuilderFactory(Func<string[], IHostBuilder> factory)
    {
        _hostBuilderFactory = factory;
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

    public IFunctionsTestHost Build()
    {
        if (_functionsAssembly == null)
        {
            throw new InvalidOperationException(
                "Functions assembly must be specified. Use WithFunctionsAssembly() or CreateBuilder<TAssembly>()");
        }

        // Create logging
        var loggerFactory = LoggerFactory.Create(builder =>
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

        // Create worker host service with the actual gRPC port
        var workerLogger = loggerFactory.CreateLogger<WorkerHostService>();
        var workerHostService = new WorkerHostService(
            workerLogger,
            actualPort,
            _functionsAssembly,
            _hostBuilderFactory);

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
            grpcHostService);
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
