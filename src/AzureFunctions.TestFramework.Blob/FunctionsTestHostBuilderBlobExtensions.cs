using Azure.Storage.Blobs;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// blob binding support (<c>[BlobInput]</c> and <c>[BlobTrigger]</c> with SDK client types).
/// </summary>
public static class FunctionsTestHostBuilderBlobExtensions
{
    /// <summary>
    /// Registers a <see cref="BlobServiceClient"/> to be used when creating blob SDK client
    /// instances (<see cref="BlobClient"/>, <c>BlockBlobClient</c>, etc.) for
    /// <c>[BlobTrigger]</c> and <c>[BlobInput]</c> parameters.
    /// <para>
    /// This registers the client as a singleton in the worker's DI container and adds
    /// <see cref="FakeBlobClientInputConverter"/> at converter index 0 so it runs before
    /// the SDK's built-in <c>BlobStorageConverter</c>.
    /// </para>
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="blobServiceClient">
    /// The <see cref="BlobServiceClient"/> to register. This can be a real client
    /// (e.g. connected to Azurite) or a mock.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithBlobServiceClient(
        this IFunctionsTestHostBuilder builder,
        BlobServiceClient blobServiceClient)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(blobServiceClient);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(blobServiceClient);

            services.PostConfigure<WorkerOptions>(options =>
            {
                options.InputConverters.RegisterAt<FakeBlobClientInputConverter>(0);
            });
        });

        return builder;
    }

    /// <summary>
    /// Registers blob paths that should inject <c>ModelBindingData</c> for <c>[BlobInput]</c>
    /// parameters targeting SDK client types (<see cref="BlobClient"/>, etc.).
    /// <para>
    /// Use this instead of <see cref="WithBlobInputContent(IFunctionsTestHostBuilder, string, BinaryData)"/>
    /// when the function parameter is a blob SDK client type. Requires
    /// <see cref="WithBlobServiceClient"/> to also be called.
    /// </para>
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="blobPaths">
    /// The blob path patterns as declared in <c>[BlobInput]</c> attributes
    /// (e.g. <c>"my-container/data.txt"</c>). These paths will receive
    /// <c>ModelBindingData</c> instead of raw bytes.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithBlobInputClient(
        this IFunctionsTestHostBuilder builder,
        params string[] blobPaths)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(blobPaths);

        return builder.WithSyntheticBindingProvider(
            new BlobInputClientSyntheticBindingProvider(blobPaths));
    }

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
