using System.Collections.Immutable;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using AzureFunctions.TestFramework.Blob;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Blob;

/// <summary>
/// Unit tests for <see cref="FakeBlobClientInputConverter"/>.
/// </summary>
public class FakeBlobClientInputConverterTests
{
    private static readonly Uri FakeServiceUri = new("https://myaccount.blob.core.windows.net");
    private static readonly BlobServiceClient ServiceClient = new(FakeServiceUri);

    private FakeBlobClientInputConverter CreateConverter(BlobServiceClient? serviceClient = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (serviceClient is not null)
            services.AddSingleton(serviceClient);
        var sp = services.BuildServiceProvider();
        return new FakeBlobClientInputConverter(sp);
    }

    // -------------------------------------------------------------------------
    // ConvertAsync — unsupported type
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_UnsupportedTargetType_ReturnsUnhandled()
    {
        var converter = CreateConverter(ServiceClient);
        var ctx = new FakeConverterContext(typeof(string), source: null);
        var result = await converter.ConvertAsync(ctx);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_NoBlobServiceClient_ReturnsUnhandled()
    {
        var converter = CreateConverter(serviceClient: null);
        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson("my-container", "my-blob.txt");
        var ctx = new FakeConverterContext(typeof(BlobClient), source: json);
        var result = await converter.ConvertAsync(ctx);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_SourceIsNull_ReturnsUnhandled()
    {
        var converter = CreateConverter(ServiceClient);
        var ctx = new FakeConverterContext(typeof(BlobClient), source: null);
        var result = await converter.ConvertAsync(ctx);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_SourceWithoutMarker_ReturnsUnhandled()
    {
        var converter = CreateConverter(ServiceClient);
        var ctx = new FakeConverterContext(typeof(BlobClient), source: """{"ContainerName":"x","BlobName":"y"}""");
        var result = await converter.ConvertAsync(ctx);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    // -------------------------------------------------------------------------
    // ConvertAsync — BlobContainerClient (no blob name needed)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConvertAsync_BlobContainerClient_ReturnsContainerClient()
    {
        var converter = CreateConverter(ServiceClient);
        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson("my-container", null);
        var ctx = new FakeConverterContext(typeof(BlobContainerClient), source: json);

        var result = await converter.ConvertAsync(ctx);

        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        Assert.IsType<BlobContainerClient>(result.Value);
    }

    // -------------------------------------------------------------------------
    // ConvertAsync — blob-level clients (blob name required)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(typeof(BlobClient))]
    [InlineData(typeof(BlockBlobClient))]
    [InlineData(typeof(PageBlobClient))]
    [InlineData(typeof(AppendBlobClient))]
    [InlineData(typeof(BlobBaseClient))]
    public async Task ConvertAsync_BlobClientTypes_ReturnCorrectClientType(Type targetType)
    {
        var converter = CreateConverter(ServiceClient);
        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson("my-container", "my-blob.txt");
        var ctx = new FakeConverterContext(targetType, source: json);

        var result = await converter.ConvertAsync(ctx);

        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        Assert.NotNull(result.Value);
        // The returned object should be assignable to the base BlobBaseClient.
        Assert.IsAssignableFrom<BlobBaseClient>(result.Value);
    }

    [Fact]
    public async Task ConvertAsync_BlobClientWithNoBlob_ReturnsFailed()
    {
        var converter = CreateConverter(ServiceClient);
        // No blob name — BlobClient requires one.
        var json = FunctionsTestHostBlobExtensions.CreateBlobClientJson("my-container", null);
        var ctx = new FakeConverterContext(typeof(BlobClient), source: json);

        var result = await converter.ConvertAsync(ctx);

        Assert.Equal(ConversionStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_MissingContainerName_ReturnsFailed()
    {
        var converter = CreateConverter(ServiceClient);
        var json = JsonSerializer.Serialize(new
        {
            Marker = FakeBlobClientInputConverter.BindingMarker,
            BlobName = "blob.txt"
        });
        var ctx = new FakeConverterContext(typeof(BlobClient), source: json);

        var result = await converter.ConvertAsync(ctx);

        Assert.Equal(ConversionStatus.Failed, result.Status);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class FakeConverterContext : ConverterContext
    {
        private readonly FunctionContext _functionContext = new MinimalFunctionContext();

        public FakeConverterContext(Type targetType, object? source)
        {
            TargetType = targetType;
            Source = source;
        }

        public override Type TargetType { get; }
        public override object? Source { get; }
        public override FunctionContext FunctionContext => _functionContext;
        public override IReadOnlyDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
    }

    private sealed class MinimalFunctionContext : FunctionContext
    {
        public override BindingContext BindingContext { get; } = null!;
        public override IInvocationFeatures Features { get; } = null!;
        public override FunctionDefinition FunctionDefinition { get; } = null!;
        public override string FunctionId { get; } = Guid.NewGuid().ToString();
        public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();
        public override string InvocationId { get; } = Guid.NewGuid().ToString();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override RetryContext RetryContext { get; } = null!;
        public override TraceContext TraceContext { get; } = null!;
    }
}
