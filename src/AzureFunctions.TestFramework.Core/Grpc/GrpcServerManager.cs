using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Manages the gRPC server that the Functions worker connects to.
/// </summary>
public class GrpcServerManager : IAsyncDisposable
{
    private readonly ILogger<GrpcServerManager> _logger;
    private readonly GrpcHostService _hostService;
    private IHost? _grpcHost;
    private int _port;

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
    /// Gets the port the gRPC server is listening on.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets the gRPC host service.
    /// </summary>
    public GrpcHostService HostService => _hostService;

    /// <summary>
    /// Starts the gRPC server.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting gRPC server on an ephemeral port");

        _grpcHost = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder
                    .UseKestrel(options =>
                    {
                        // Bind to port 0 so the OS assigns a free port atomically,
                        // avoiding the TOCTOU race of FindAvailablePort + manual bind.
                        options.ListenAnyIP(0, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddGrpc(options =>
                        {
                            options.Interceptors.Add<GrpcLoggingInterceptor>();
                        });
                        services.AddSingleton(_hostService);
                        services.AddSingleton<GrpcLoggingInterceptor>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<GrpcHostService>();
                        });
                    });
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
                logging.AddFilter("Grpc", LogLevel.Warning);
                logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            })
            .Build();

        await _grpcHost.StartAsync(cancellationToken);

        // Read the port assigned by the OS (port 0 binding).
        var server = _grpcHost.Services.GetRequiredService<IServer>();
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        var address = addressesFeature!.Addresses.First();
        _port = new Uri(address).Port;

        _logger.LogInformation("gRPC server started on port {Port}", _port);
    }

    /// <summary>
    /// Stops the gRPC server.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_grpcHost != null)
        {
            _logger.LogInformation("Stopping gRPC server");
            await _grpcHost.StopAsync(cancellationToken);
            _logger.LogInformation("gRPC server stopped");
        }
    }

    /// <summary>
    /// Asynchronously stops and disposes the underlying gRPC host.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_grpcHost != null)
        {
            await _grpcHost.StopAsync();
            _grpcHost.Dispose();
        }
    }

}
