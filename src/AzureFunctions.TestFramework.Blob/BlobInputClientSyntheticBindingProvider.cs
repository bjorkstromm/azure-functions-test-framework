using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Injects synthetic <c>blob</c> input binding data as a JSON string
/// for functions that declare a <c>[BlobInput]</c> parameter targeting an SDK client type
/// (e.g. <c>BlobClient</c>, <c>BlockBlobClient</c>).
/// <para>
/// Unlike <see cref="BlobInputSyntheticBindingProvider"/> (which injects raw bytes for
/// content-typed parameters), this provider emits a JSON string so the
/// <see cref="FakeBlobClientInputConverter"/> can construct the appropriate client.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderBlobExtensions.WithBlobInputClient(IFunctionsTestHostBuilder, string[])"/>
/// and ensure <see cref="FunctionsTestHostBuilderBlobExtensions.WithBlobServiceClient"/> is also called.
/// </para>
/// </summary>
public sealed class BlobInputClientSyntheticBindingProvider : ISyntheticBindingProvider
{
    private readonly HashSet<string> _registeredPaths;

    /// <summary>
    /// Initialises a new instance with the specified blob paths that should receive
    /// client binding JSON instead of raw bytes.
    /// </summary>
    /// <param name="blobPaths">
    /// The blob path patterns as declared in <c>[BlobInput]</c> attributes
    /// (e.g. <c>"my-container/data.txt"</c>). Lookups are case-insensitive.
    /// </param>
    public BlobInputClientSyntheticBindingProvider(IEnumerable<string> blobPaths)
    {
        ArgumentNullException.ThrowIfNull(blobPaths);
        _registeredPaths = new HashSet<string>(blobPaths, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public string BindingType => "blob";

    /// <inheritdoc/>
    public FunctionBindingData? CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
    {
        var direction = bindingConfig.TryGetProperty("direction", out var dir) ? dir.GetString() : null;
        if (direction is not null && !direction.Equals("In", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var blobPath = bindingConfig.TryGetProperty("blobPath", out var bp) ? bp.GetString() : null;

        if (blobPath is null || !_registeredPaths.Contains(blobPath))
        {
            return null;
        }

        ParseBlobPath(blobPath, out var containerName, out var blobName);

        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson(containerName, blobName);

        return FunctionBindingData.WithJson(parameterName, json);
    }

    /// <summary>
    /// Splits a blob path like <c>"container/path/to/blob.txt"</c> into container name
    /// and blob name. The first segment is the container; the rest is the blob name.
    /// </summary>
    internal static void ParseBlobPath(string blobPath, out string containerName, out string? blobName)
    {
        var separatorIndex = blobPath.IndexOf('/');
        if (separatorIndex < 0)
        {
            containerName = blobPath;
            blobName = null;
        }
        else
        {
            containerName = blobPath[..separatorIndex];
            blobName = blobPath[(separatorIndex + 1)..];
        }
    }
}
