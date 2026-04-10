using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Injects synthetic <c>blob</c> input binding data into every invocation of functions that
/// declare a <c>[BlobInput]</c> parameter.
/// <para>
/// The real Azure Functions host reads the blob content from storage and passes it as bytes
/// in the <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects pre-configured
/// content so that the worker's blob input converters can construct the target type
/// (e.g. <c>string</c>, <c>byte[]</c>, <c>Stream</c>).
/// </para>
/// <para>
/// The source-generated binding metadata for <c>[BlobInput]</c> uses type <c>"blob"</c> with
/// <c>direction: "In"</c>. This provider matches on <c>"blob"</c> and skips output-direction
/// bindings automatically.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderBlobExtensions.WithBlobInputContent(IFunctionsTestHostBuilder, string, BinaryData)"/>
/// or
/// <see cref="FunctionsTestHostBuilderBlobExtensions.WithBlobInputContent(IFunctionsTestHostBuilder, IReadOnlyDictionary{string, BinaryData})"/>.
/// </para>
/// <para>
/// <b>Note:</b> Register all <c>[BlobInput]</c> paths using a single call to keep the provider list compact.
/// If multiple <see cref="BlobInputSyntheticBindingProvider"/> instances are registered for the same
/// function, all of them will inject binding entries for the same parameter, which may cause
/// unexpected behavior in the worker.
/// </para>
/// </summary>
public sealed class BlobInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    private readonly IReadOnlyDictionary<string, BinaryData> _contentByPath;

    /// <summary>
    /// Initialises a new instance of <see cref="BlobInputSyntheticBindingProvider"/> with the
    /// specified blob-path-to-content mappings.
    /// </summary>
    /// <param name="contentByPath">
    /// A dictionary mapping blob path patterns (as declared in the <c>[BlobInput]</c> attribute,
    /// e.g. <c>"my-container/data.txt"</c> or <c>"my-container/{queueTrigger}"</c>) to the
    /// <see cref="BinaryData"/> content to inject.  Lookups are case-insensitive.
    /// </param>
    public BlobInputSyntheticBindingProvider(IReadOnlyDictionary<string, BinaryData> contentByPath)
    {
        ArgumentNullException.ThrowIfNull(contentByPath);
        _contentByPath = contentByPath;
    }

    /// <inheritdoc/>
    public string BindingType => "blob";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
    {
        // Only inject data for input bindings (direction "In").
        // Output bindings (direction "Out") do not need synthetic input data.
        var direction = bindingConfig.TryGetProperty("direction", out var dir) ? dir.GetString() : null;
        if (direction is not null &&
            !direction.Equals("In", StringComparison.OrdinalIgnoreCase))
        {
            // Return empty bytes for output/return bindings — the worker ignores extra InputData
            // entries for non-input parameters, so this is safe.
            return FunctionBindingData.WithBytes(parameterName, []);
        }

        var blobPath = bindingConfig.TryGetProperty("blobPath", out var bp) ? bp.GetString() : null;

        if (blobPath is not null && _contentByPath.TryGetValue(blobPath, out var content))
        {
            return FunctionBindingData.WithBytes(parameterName, content.ToArray());
        }

        // No registered content for this binding path — inject empty bytes.
        // The worker will surface this as an empty string / empty byte array / empty Stream.
        return FunctionBindingData.WithBytes(parameterName, []);
    }
}
