using System.Text.Json;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Default values used when synthesizing Durable client binding payloads for in-process tests.
/// </summary>
public static class DurableClientBindingDefaults
{
    /// <summary>
    /// Gets the synthetic gRPC endpoint used to key fake Durable client bindings.
    /// </summary>
    public const string RpcBaseUrl = "http://127.0.0.1:17071";

    /// <summary>
    /// Gets the synthetic HTTP endpoint used for Durable management URLs in test payloads.
    /// </summary>
    public const string HttpBaseUrl = "http://127.0.0.1:17070";

    /// <summary>
    /// Creates a Durable client binding payload that matches the format expected by the official Durable converter.
    /// </summary>
    /// <param name="taskHub">Optional task hub name.</param>
    /// <param name="connectionName">Optional connection name.</param>
    /// <param name="requiredQueryStringParameters">Optional required query string parameters.</param>
    /// <param name="maxGrpcMessageSizeInBytes">Optional max gRPC message size.</param>
    /// <param name="grpcHttpClientTimeout">Optional gRPC HTTP client timeout.</param>
    /// <returns>The serialized payload.</returns>
    public static string CreatePayload(
        string? taskHub = null,
        string? connectionName = null,
        string? requiredQueryStringParameters = null,
        int maxGrpcMessageSizeInBytes = int.MaxValue,
        TimeSpan? grpcHttpClientTimeout = null)
    {
        return JsonSerializer.Serialize(new
        {
            rpcBaseUrl = RpcBaseUrl,
            taskHubName = taskHub ?? string.Empty,
            connectionName = connectionName ?? string.Empty,
            requiredQueryStringParameters = requiredQueryStringParameters ?? string.Empty,
            httpBaseUrl = HttpBaseUrl,
            maxGrpcMessageSizeInBytes,
            grpcHttpClientTimeout = JsonSerializer.Serialize(grpcHttpClientTimeout ?? TimeSpan.FromSeconds(100))
        });
    }
}
