using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Manages the gRPC server that the Functions worker connects to.
/// Uses <see cref="TestServer"/> (in-memory) instead of Kestrel so no TCP port is bound
/// and there are no Windows Firewall prompts.
/// </summary>
public class GrpcServerManager : IAsyncDisposable
{
    private readonly ILogger<GrpcServerManager> _logger;
    private readonly GrpcHostService _hostService;
    private IHost? _grpcHost;
    private HttpMessageHandler? _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcServerManager"/> class.
    /// </summary>
    /// <param name="logger">The logger used for server lifecycle messages.</param>
    /// <param name="hostService">The gRPC host service exposed by the server.</param>
    public GrpcServerManager(
        ILogger<GrpcServerManager> logger,
        GrpcHostService hostService)
    {
        _logger = logger;
        _hostService = hostService;
    }

    /// <summary>
    /// Gets the in-memory <see cref="HttpMessageHandler"/> that routes gRPC traffic to the
    /// in-process server without a TCP connection.
    /// Available after <see cref="StartAsync"/> completes.
    /// </summary>
    public HttpMessageHandler Handler => _handler
        ?? throw new InvalidOperationException("GrpcServerManager has not been started");

    /// <summary>
    /// Gets the gRPC host service.
    /// </summary>
    public GrpcHostService HostService => _hostService;

    /// <summary>
    /// Starts the in-memory gRPC server backed by <see cref="TestServer"/>.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting in-memory gRPC server (TestServer)");

        var hostService = _hostService;

        _grpcHost = await new HostBuilder()
            .ConfigureWebHost(wb =>
            {
                wb.UseTestServer();
                wb.ConfigureServices(services =>
                {
                    services.AddGrpc(options =>
                    {
                        options.Interceptors.Add<GrpcLoggingInterceptor>();
                    });
                    services.AddSingleton(hostService);
                    services.AddSingleton<GrpcLoggingInterceptor>();
                });
                wb.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<GrpcHostService>();
                    });
                });
                wb.ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                    logging.AddFilter("Grpc", LogLevel.Warning);
                    logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
                });
            })
            .StartAsync(cancellationToken);

        _handler = _grpcHost.GetTestServer().CreateHandler();

        _logger.LogInformation("In-memory gRPC server started");
    }

    /// <summary>
    /// Stops the in-memory gRPC server.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping in-memory gRPC server");
        if (_grpcHost != null)
        {
            await _grpcHost.StopAsync(cancellationToken);
        }
        _logger.LogInformation("In-memory gRPC server stopped");
    }

    /// <summary>
    /// Asynchronously disposes the server.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_grpcHost != null)
        {
            await _grpcHost.StopAsync();
            _grpcHost.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}


