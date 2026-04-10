using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// <c>[BlobInput]</c> binding support.
/// </summary>
public static class FunctionsTestHostBuilderBlobExtensions
{
    /// <summary>
    /// Registers fake blob content to be injected for every function invocation that declares
    /// a <c>[BlobInput(<paramref name="blobPath"/>)]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="blobPath">
    /// The blob path exactly as declared in the <c>[BlobInput]</c> attribute
    /// (e.g. <c>"my-container/data.txt"</c> or <c>"my-container/{queueTrigger}"</c>).
    /// Matching is case-insensitive.
    /// </param>
    /// <param name="content">The blob content to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// To register content for multiple <c>[BlobInput]</c> bindings, prefer the dictionary overload
    /// <see cref="WithBlobInputContent(IFunctionsTestHostBuilder, IReadOnlyDictionary{string, BinaryData})"/>
    /// so that a single <see cref="BlobInputSyntheticBindingProvider"/> is registered.  Calling this
    /// overload multiple times adds a separate provider per path, which may inject duplicate binding
    /// entries for functions that have more than one <c>[BlobInput]</c> parameter.
    /// </remarks>
    public static IFunctionsTestHostBuilder WithBlobInputContent(
        this IFunctionsTestHostBuilder builder,
        string blobPath,
        BinaryData content)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(blobPath);
        ArgumentNullException.ThrowIfNull(content);

        return builder.WithSyntheticBindingProvider(
            new BlobInputSyntheticBindingProvider(
                new Dictionary<string, BinaryData>(StringComparer.OrdinalIgnoreCase)
                {
                    [blobPath] = content
                }));
    }

    /// <summary>
    /// Registers fake blob content for multiple <c>[BlobInput]</c> bindings to be injected on
    /// every function invocation that declares a matching <c>[BlobInput]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="contentByPath">
    /// A dictionary mapping blob path patterns (as declared in the <c>[BlobInput]</c> attribute)
    /// to the <see cref="BinaryData"/> content to inject.  Lookups are case-insensitive.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithBlobInputContent(
        this IFunctionsTestHostBuilder builder,
        IReadOnlyDictionary<string, BinaryData> contentByPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(contentByPath);

        return builder.WithSyntheticBindingProvider(new BlobInputSyntheticBindingProvider(contentByPath));
    }
}
