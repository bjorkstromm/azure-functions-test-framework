using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Core;

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

    /// <summary>
    /// Initializes a new instance of <see cref="FunctionsWebApplicationFactory{TProgram}"/>
    /// and immediately starts the in-process gRPC host server.
    /// </summary>
    public FunctionsWebApplicationFactory()
    {
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

        // Start the gRPC server eagerly so the port is known before the host is built.
        _grpcServerManager.StartAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the route-to-functionId map populated after the worker connects and loads functions.
    /// Key format is <c>"{METHOD}:{route}"</c>, e.g. <c>"GET:todos"</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> FunctionRouteMap => _grpcHostService.FunctionRouteMap;

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Register a startup filter that automatically injects the x-ms-invocation-id header
        // when it is absent. In production the Azure Functions host always adds this header;
        // in tests we generate one so the FunctionsHttpProxyingMiddleware doesn't reject the
        // request with "Expected correlation id header not present".
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, InvocationIdStartupFilter>();
        });
    }

    /// <inheritdoc/>
    protected override IHost CreateHost(IHostBuilder builder)
    {
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
        Task.Run(WaitForFunctionsReadyAsync).GetAwaiter().GetResult();

        return host;
    }

    private async Task WaitForFunctionsReadyAsync()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;
        while (!_grpcHostService.IsConnected && DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(200);
        }

        if (!_grpcHostService.IsConnected)
        {
            throw new FunctionsTestHostException("Worker did not connect to gRPC server within timeout");
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await _grpcHostService.WaitForFunctionsLoadedAsync().WaitAsync(cts.Token);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _grpcServerManager.StopAsync().GetAwaiter().GetResult();
            _grpcServerManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _loggerFactory.Dispose();
        }

        base.Dispose(disposing);
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
}
