using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Blob;

/// <summary>
/// Converts blob input bindings targeting SDK client types (<see cref="BlobClient"/>,
/// <see cref="BlockBlobClient"/>, etc.) by resolving a <see cref="BlobServiceClient"/>
/// from DI and creating the appropriate client for the requested container and blob.
/// <para>
/// The binding data is expected as a JSON string with <c>ContainerName</c> and <c>BlobName</c>
/// properties, sent by <see cref="FunctionsTestHostBlobExtensions"/> (trigger) or
/// <see cref="BlobInputClientSyntheticBindingProvider"/> (input binding).
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderBlobExtensions.WithBlobServiceClient"/>.
/// </para>
/// </summary>
[SupportedTargetType(typeof(BlobClient))]
[SupportedTargetType(typeof(BlockBlobClient))]
[SupportedTargetType(typeof(PageBlobClient))]
[SupportedTargetType(typeof(AppendBlobClient))]
[SupportedTargetType(typeof(BlobBaseClient))]
[SupportedTargetType(typeof(BlobContainerClient))]
public sealed class FakeBlobClientInputConverter : IInputConverter
{
    /// <summary>
    /// Marker prefix embedded in the JSON payload so the converter can distinguish
    /// framework-provided blob binding data from arbitrary string sources.
    /// </summary>
    internal const string BindingMarker = "__FakeBlobClient__";

    private static readonly HashSet<string> _supportedTypeNames =
    [
        typeof(BlobClient).FullName!,
        typeof(BlockBlobClient).FullName!,
        typeof(PageBlobClient).FullName!,
        typeof(AppendBlobClient).FullName!,
        typeof(BlobBaseClient).FullName!,
        typeof(BlobContainerClient).FullName!,
    ];

    private readonly BlobServiceClient? _blobServiceClient;
    private readonly ILogger<FakeBlobClientInputConverter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeBlobClientInputConverter"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the blob service client and logger.</param>
    public FakeBlobClientInputConverter(IServiceProvider serviceProvider)
    {
        _blobServiceClient = serviceProvider.GetService<BlobServiceClient>();
        _logger = serviceProvider.GetRequiredService<ILogger<FakeBlobClientInputConverter>>();
    }

    /// <inheritdoc/>
    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        // Only handle client target types — let byte-based types (string, Stream, etc.) fall through.
        if (context.TargetType.FullName is null || !_supportedTypeNames.Contains(context.TargetType.FullName))
        {
            return ValueTask.FromResult(ConversionResult.Unhandled());
        }

        if (_blobServiceClient is null)
        {
            _logger.LogWarning(
                "BlobServiceClient not registered in DI. Call WithBlobServiceClient() on the test host builder. Falling through to SDK converter.");
            return ValueTask.FromResult(ConversionResult.Unhandled());
        }

        // The framework sends blob binding info as a JSON string with a marker prefix
        // (via FunctionBindingData.WithJson). This avoids the SDK's deferred-binding
        // pipeline which routes ModelBindingData to BlobStorageConverter first.
        if (context.Source is not string jsonSource || !jsonSource.Contains(BindingMarker))
        {
            return ValueTask.FromResult(ConversionResult.Unhandled());
        }

        try
        {
            var blobData = JsonSerializer.Deserialize<BlobBindingContent>(
                jsonSource,
                _jsonOptions);

            if (blobData is null || string.IsNullOrEmpty(blobData.ContainerName))
            {
                return ValueTask.FromResult(ConversionResult.Failed(
                    new InvalidOperationException("ContainerName is required in blob binding data.")));
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(blobData.ContainerName);
            var result = CreateClient(context.TargetType, containerClient, blobData.BlobName);

            _logger.LogInformation(
                "Created {ClientType} for container '{Container}', blob '{Blob}' via FakeBlobClientInputConverter",
                context.TargetType.Name, blobData.ContainerName, blobData.BlobName);

            return ValueTask.FromResult(ConversionResult.Success(result));
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(ConversionResult.Failed(ex));
        }
    }

    private static readonly Dictionary<string, Func<BlobContainerClient, string, object>> _blobClientFactories = new()
    {
        [typeof(BlobClient).FullName!] = (c, n) => c.GetBlobClient(n),
        [typeof(BlockBlobClient).FullName!] = (c, n) => c.GetBlockBlobClient(n),
        [typeof(PageBlobClient).FullName!] = (c, n) => c.GetPageBlobClient(n),
        [typeof(AppendBlobClient).FullName!] = (c, n) => c.GetAppendBlobClient(n),
        [typeof(BlobBaseClient).FullName!] = (c, n) => c.GetBlobBaseClient(n),
    };

    private static object CreateClient(Type targetType, BlobContainerClient containerClient, string? blobName)
    {
        var targetFullName = targetType.FullName;

        if (targetFullName == typeof(BlobContainerClient).FullName)
            return containerClient;

        if (string.IsNullOrEmpty(blobName))
            throw new InvalidOperationException("BlobName is required when binding to a blob client type.");

        if (targetFullName is not null && _blobClientFactories.TryGetValue(targetFullName, out var factory))
            return factory(containerClient, blobName);

        throw new InvalidOperationException($"Unsupported blob client type: {targetType.FullName}");
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal sealed class BlobBindingContent
    {
        public string? Marker { get; set; }
        public string? ContainerName { get; set; }
        public string? BlobName { get; set; }
    }
}
