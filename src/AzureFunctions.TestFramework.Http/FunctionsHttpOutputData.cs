namespace AzureFunctions.TestFramework.Http;

/// <summary>
/// Provides access to non-HTTP output bindings (e.g. <c>[QueueOutput]</c>, <c>[BlobOutput]</c>)
/// captured alongside an HTTP trigger's response when a function mixes an HTTP trigger with
/// additional output bindings on the same multi-output result type.
/// </summary>
/// <remarks>
/// Only populated in direct gRPC mode (<c>ConfigureFunctionsWorkerDefaults()</c>). In ASP.NET Core
/// integration mode (<c>ConfigureFunctionsWebApplication()</c>) the invocation is dispatched through
/// the worker's in-memory <c>TestServer</c> pipeline rather than a gRPC <c>InvocationResponse</c>,
/// so additional output bindings are not captured here; use a DI-injected fake/mock client instead.
/// </remarks>
public static class FunctionsHttpOutputData
{
    /// <summary>
    /// The <see cref="HttpRequestOptions"/> key under which captured output binding data is stored
    /// on the originating <see cref="HttpRequestMessage"/>.
    /// </summary>
    public static readonly HttpRequestOptionsKey<IReadOnlyDictionary<string, object?>> Key =
        new("AzureFunctions.TestFramework.Http.OutputData");

    /// <summary>
    /// Reads the output binding data (if any) captured for the request that produced this response.
    /// Returns an empty dictionary when none was captured (e.g. ASP.NET Core integration mode, or
    /// the invoked function has no additional output bindings beyond its HTTP response).
    /// </summary>
    public static IReadOnlyDictionary<string, object?> GetOutputData(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.RequestMessage?.Options.TryGetValue(Key, out var outputData) == true && outputData != null)
        {
            return outputData;
        }

        return new Dictionary<string, object?>();
    }
}
