using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Extension methods for invoking blob-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostBlobExtensions
{

    /// <summary>
    /// Invokes a blob-triggered function by name with the specified blob content.
    /// Use this overload when the trigger parameter is a content type (<c>string</c>,
    /// <c>Stream</c>, <c>byte[]</c>, <c>BinaryData</c>).
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the blob function (case-insensitive).</param>
    /// <param name="content">
    /// The blob content to pass to the function as the trigger input.
    /// </param>
    /// <param name="blobName">
    /// Optional name of the blob (e.g. <c>"myblob.txt"</c>).
    /// Passed to the worker as trigger metadata so blob-name–bound parameters are populated.
    /// </param>
    /// <param name="containerName">
    /// Optional name of the blob container.  When provided together with <paramref name="blobName"/>,
    /// the full path metadata is made available to the function.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeBlobAsync(
        this IFunctionsTestHost host,
        string functionName,
        BinaryData content,
        string? blobName = null,
        string? containerName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        string? triggerMetadataJson = null;
        if (!string.IsNullOrEmpty(blobName) || !string.IsNullOrEmpty(containerName))
        {
            triggerMetadataJson = JsonSerializer.Serialize(new
            {
                BlobName = blobName,
                ContainerName = containerName
            });
        }

        var context = new FunctionInvocationContext
        {
            TriggerType = "blobTrigger",
            InputData =
            {
                ["$blobContentBytes"] = content.ToArray()
            }
        };

        if (triggerMetadataJson != null)
        {
            context.InputData["$triggerMetadata"] = triggerMetadataJson;
        }

        return host.Invoker.InvokeAsync(functionName, context, CreateBytesBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes a blob-triggered function by name, sending binding data so the
    /// Worker SDK can create a blob SDK client (<c>BlobClient</c>, <c>BlockBlobClient</c>, etc.).
    /// <para>
    /// Use this overload when the trigger parameter is a blob client type.
    /// Requires <see cref="FunctionsTestHostBuilderBlobExtensions.WithBlobServiceClient"/>
    /// to be called during host setup.
    /// </para>
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the blob function (case-insensitive).</param>
    /// <param name="containerName">The blob container name.</param>
    /// <param name="blobName">The blob name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeBlobAsync(
        this IFunctionsTestHost host,
        string functionName,
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrEmpty(functionName);
        ArgumentException.ThrowIfNullOrEmpty(containerName);
        ArgumentException.ThrowIfNullOrEmpty(blobName);

        var bindingJson = CreateBlobClientJson(containerName, blobName);

        var triggerMetadataJson = JsonSerializer.Serialize(new
        {
            BlobName = blobName,
            ContainerName = containerName
        });

        var context = new FunctionInvocationContext
        {
            TriggerType = "blobTrigger",
            InputData =
            {
                ["$blobClientJson"] = bindingJson
            }
        };

        context.InputData["$triggerMetadata"] = triggerMetadataJson;

        return host.Invoker.InvokeAsync(functionName, context, CreateClientBindingData, cancellationToken);
    }

    /// <summary>
    /// Creates the JSON payload used by <see cref="FakeBlobClientInputConverter"/> to construct
    /// blob SDK client instances. Includes a marker so the converter can identify it.
    /// </summary>
    internal static string CreateBlobClientJson(string containerName, string? blobName)
    {
        return JsonSerializer.Serialize(new
        {
            Marker = FakeBlobClientInputConverter.BindingMarker,
            ContainerName = containerName,
            BlobName = blobName
        });
    }

    private static TriggerBindingData CreateBytesBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var contentBytes = context.InputData.TryGetValue("$blobContentBytes", out var b) && b is byte[] bytes
            ? bytes
            : Array.Empty<byte>();

        var triggerMetadata = context.InputData.TryGetValue("$triggerMetadata", out var m)
            ? m?.ToString()
            : null;

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithBytes(function.ParameterName, contentBytes)],
            TriggerMetadataJson = string.IsNullOrEmpty(triggerMetadata)
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal)
                    { [function.ParameterName] = triggerMetadata }
        };
    }

    private static TriggerBindingData CreateClientBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var bindingJson = context.InputData.TryGetValue("$blobClientJson", out var j) ? j?.ToString() : null;

        var triggerMetadata = context.InputData.TryGetValue("$triggerMetadata", out var m)
            ? m?.ToString()
            : null;

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, bindingJson ?? "{}")],
            TriggerMetadataJson = string.IsNullOrEmpty(triggerMetadata)
                ? null
                : new Dictionary<string, string>(StringComparer.Ordinal)
                    { [function.ParameterName] = triggerMetadata }
        };
    }
}
