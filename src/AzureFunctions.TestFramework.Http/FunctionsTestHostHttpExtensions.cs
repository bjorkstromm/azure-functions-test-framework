using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Http;

/// <summary>
/// Extension methods for <see cref="IFunctionsTestHost"/> that add HTTP client support.
/// </summary>
public static class FunctionsTestHostHttpExtensions
{
    /// <summary>
    /// Creates an <see cref="HttpClient"/> configured to invoke functions in-process.
    /// Similar to <c>WebApplicationFactory.CreateClient()</c>.
    /// When the worker uses <c>ConfigureFunctionsWebApplication()</c>, requests are forwarded
    /// to the worker's in-memory TestServer (ASP.NET Core integration mode).
    /// Otherwise requests are dispatched directly via the gRPC InvocationRequest channel.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <returns>A configured <see cref="HttpClient"/> ready to send requests to the functions worker.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the host has not been started yet.</exception>
    public static HttpClient CreateHttpClient(this IFunctionsTestHost host)
    {
        if (host is not IHttpSupportedTestHost httpHost)
        {
            throw new InvalidOperationException(
                $"The test host does not implement {nameof(IHttpSupportedTestHost)}. " +
                "Ensure you are using FunctionsTestHost from AzureFunctions.TestFramework.Core.");
        }

        // ASP.NET Core integration mode: forward HTTP requests to the worker's HTTP server (in-memory TestServer).
        var workerHttpHandler = httpHost.WorkerHttpHandler;

        if (workerHttpHandler != null)
        {
            return new HttpClient(new AspNetCoreForwardingHandler(workerHttpHandler), disposeHandler: true)
            {
                BaseAddress = new Uri("http://localhost/"),
                Timeout = httpHost.InvocationTimeout
            };
        }

        // gRPC-direct mode (ConfigureFunctionsWorkerDefaults): dispatch via InvocationRequest.
        return new HttpClient(
            new FunctionsHttpMessageHandler(
                httpHost.GrpcHostService,
                httpHost.GrpcHostService.RouteMatcher,
                httpHost.RoutePrefix),
            disposeHandler: true)
        {
            BaseAddress = new Uri($"http://localhost/{httpHost.RoutePrefix}/"),
            Timeout = httpHost.InvocationTimeout
        };
    }
}
