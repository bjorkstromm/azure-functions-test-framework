using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly List<Action<IServiceCollection>> _serviceConfigurators = new();
    private IHost? _workerHost;
    private bool _isInitialized;

    public WorkerHostService(
        ILogger<WorkerHostService> logger,
        int grpcPort,
        Assembly functionsAssembly)
    {
        _logger = logger;
        _grpcPort = grpcPort;
        _functionsAssembly = functionsAssembly;
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
        
        _logger.LogInformation("Configuring worker to connect to {GrpcUri}", grpcUri);
        
        // IMPORTANT: Set environment variables BEFORE creating HostBuilder!
        // ConfigureFunctionsWorkerDefaults calls AddEnvironmentVariables() which will read these
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME_VERSION", "8.0");
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Development");
        
        var hostBuilder = new HostBuilder();

        // The worker's ConfigureFunctionsWorkerDefaults will:
        // 1. Add AZURE_FUNCTIONS_* environment variables via ConfigureHostConfiguration
        // 2. Add all environment variables via ConfigureAppConfiguration
        // 3. Add command-line args with switch mappings
        //
        // So we configure BEFORE calling ConfigureFunctionsWorkerDefaults
        hostBuilder.ConfigureHostConfiguration(config =>
        {
            // These get added BEFORE the worker's own configuration
            var configValues = new Dictionary<string, string?>
            {
                ["Functions:Worker:HostEndpoint"] = grpcUri,
                ["Functions:Worker:WorkerId"] = workerId,
                ["Functions:Worker:RequestId"] = requestId,
                ["Functions:Worker:GrpcMaxMessageLength"] = "2147483647"
            };

            config.AddInMemoryCollection(configValues);
        });
        
        // Configure app configuration with the same values
        hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            var configValues = new Dictionary<string, string?>
            {
                ["Functions:Worker:HostEndpoint"] = grpcUri,
                ["Functions:Worker:WorkerId"] = workerId,
                ["Functions:Worker:RequestId"] = requestId,
                ["Functions:Worker:GrpcMaxMessageLength"] = "2147483647",
                ["AzureWebJobsScriptRoot"] = Path.GetDirectoryName(_functionsAssembly.Location) ?? string.Empty,
                ["FUNCTIONS_WORKER_DIRECTORY"] = Path.GetDirectoryName(_functionsAssembly.Location) ?? string.Empty
            };

            config.AddInMemoryCollection(configValues);
        });

        // Configure the Functions Worker
        hostBuilder.ConfigureFunctionsWorkerDefaults((context, builder) =>
        {
            // Apply user service configurations (for mocking/overriding services)
            foreach (var configurator in _serviceConfigurators)
            {
                configurator(builder.Services);
            }
        });
        
        // IMPORTANT: Add our configuration AFTER ConfigureFunctionsWorkerDefaults
        // so our values override any defaults from environment variables
        hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            var configValues = new Dictionary<string, string?>
            {
                ["Functions:Worker:HostEndpoint"] = grpcUri,
                ["Functions:Worker:WorkerId"] = workerId,
                ["Functions:Worker:RequestId"] = requestId,
                ["Functions:Worker:GrpcMaxMessageLength"] = "2147483647"
            };

            // Add with high priority so they override environment variables
            config.AddInMemoryCollection(configValues);
        });

        // Suppress unnecessary logging during tests
        hostBuilder.ConfigureLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Microsoft.Azure.Functions.Worker", LogLevel.Warning);
            logging.AddFilter("Azure.Core", LogLevel.Warning);
        });

        return hostBuilder.Build();
    }
}
