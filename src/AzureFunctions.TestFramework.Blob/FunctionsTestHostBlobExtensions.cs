using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Extension methods for invoking blob-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostBlobExtensions
{
    /// <summary>
    /// Invokes a blob-triggered function by name with the specified blob content.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the blob function (case-insensitive).</param>
    /// <param name="content">
    /// The blob content to pass to the function as the trigger input.
    /// </param>
    /// <param name="blobName">
    /// Optional name of the blob (e.g. <c>"mycontainer/myblob.txt"</c>).
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
            triggerMetadataJson = System.Text.Json.JsonSerializer.Serialize(new
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

        return host.Invoker.InvokeAsync(functionName, context, cancellationToken);
    }
}
