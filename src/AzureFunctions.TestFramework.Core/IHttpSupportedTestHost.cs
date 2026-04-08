using AzureFunctions.TestFramework.Core.Grpc;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Marker interface implemented by <see cref="FunctionsTestHost"/> that exposes the internal
/// components needed by <c>AzureFunctions.TestFramework.Http</c> to create an HTTP client.
/// </summary>
public interface IHttpSupportedTestHost
{
    /// <summary>
    /// Gets the worker's HTTP message handler when running in ASP.NET Core / Kestrel mode,
    /// or <see langword="null"/> when running in direct gRPC mode.
    /// </summary>
    HttpMessageHandler? WorkerHttpHandler { get; }

    /// <summary>
    /// Gets the <see cref="GrpcHostService"/> used by the test host.
    /// </summary>
    GrpcHostService GrpcHostService { get; }

    /// <summary>
    /// Gets the HTTP route prefix (e.g. <c>"api"</c> or <c>"v1"</c>).
    /// </summary>
    string RoutePrefix { get; }

    /// <summary>
    /// Gets the per-invocation timeout.
    /// </summary>
    TimeSpan InvocationTimeout { get; }
}
