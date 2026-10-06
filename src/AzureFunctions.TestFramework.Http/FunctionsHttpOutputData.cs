using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;

namespace AzureFunctions.TestFramework.Http;

/// <summary>
/// Provides access to non-HTTP output bindings (e.g. <c>[QueueOutput]</c>, <c>[BlobOutput]</c>)
/// captured alongside an HTTP trigger's response when a function mixes an HTTP trigger with
/// additional output bindings on the same multi-output result type.
/// </summary>
/// <remarks>
/// Supported in both direct gRPC and ASP.NET Core integration modes. ASP.NET Core requests with
/// additional output bindings are buffered until the worker's invocation response is available.
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
    /// Returns an empty dictionary when the invoked function has no additional output bindings
    /// beyond its HTTP response.
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

    internal static void Capture(
        HttpResponseMessage response, HttpRequestMessage request, InvocationResponse? invocationResponse,
        string? httpOutputBindingName = null)
    {
        response.RequestMessage = request;
        httpOutputBindingName ??= invocationResponse?.OutputData
            .FirstOrDefault(p => p.Data?.Http != null)?.Name;
        var outputData = GrpcHostService.ExtractOutputData(invocationResponse, httpOutputBindingName);
        request.Options.Set(Key, outputData);
    }
}
