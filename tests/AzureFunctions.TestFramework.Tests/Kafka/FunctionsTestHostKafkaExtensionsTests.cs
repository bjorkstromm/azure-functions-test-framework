using System.Reflection;
using System.Text;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Kafka;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Kafka;

/// <summary>
/// Represents this type.
/// </summary>
public class FunctionsTestHostKafkaExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "KafkaFunc", "kafkaTrigger", "kafkaItem");

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaAsync_String_UsesJsonBinding()
    {
        var host = new FakeHost(FakeRegistration);

        await FunctionsTestHostKafkaExtensions.InvokeKafkaAsync(host, "KafkaFunc", "hello", CancellationToken.None);

        Assert.NotNull(host.LastBindingData);
        Assert.Equal("kafkaItem", host.LastBindingData!.InputData[0].Name);
        Assert.Equal("hello", host.LastBindingData.InputData[0].Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_String_UsesJsonArrayBinding()
    {
        var host = new FakeHost(FakeRegistration);

        await FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", new[] { "one", "two" }, CancellationToken.None);

        Assert.NotNull(host.LastBindingData);
        using var doc = JsonDocument.Parse(host.LastBindingData!.InputData[0].Json!);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal("one", doc.RootElement[0].GetString());
        Assert.Equal("two", doc.RootElement[1].GetString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_String_EmptyBatch_Throws()
    {
        var host = new FakeHost(FakeRegistration);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", Array.Empty<string>(), CancellationToken.None));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaAsync_Bytes_UsesBytesBinding()
    {
        var host = new FakeHost(FakeRegistration);
        var payload = Encoding.UTF8.GetBytes("bytes");

        await FunctionsTestHostKafkaExtensions.InvokeKafkaAsync(host, "KafkaFunc", payload, CancellationToken.None);

        Assert.NotNull(host.LastBindingData);
        Assert.Equal(payload, host.LastBindingData!.InputData[0].Bytes);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_Bytes_UsesBase64JsonArray()
    {
        var host = new FakeHost(FakeRegistration);
        var payloads = new[]
        {
            Encoding.UTF8.GetBytes("a"),
            Encoding.UTF8.GetBytes("b")
        };

        await FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", payloads, CancellationToken.None);

        Assert.NotNull(host.LastBindingData);
        using var doc = JsonDocument.Parse(host.LastBindingData!.InputData[0].Json!);
        Assert.Equal(Convert.ToBase64String(payloads[0]), doc.RootElement[0].GetString());
        Assert.Equal(Convert.ToBase64String(payloads[1]), doc.RootElement[1].GetString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_Bytes_EmptyBatch_Throws()
    {
        var host = new FakeHost(FakeRegistration);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", Array.Empty<byte[]>(), CancellationToken.None));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaAsync_Record_UsesModelBindingData()
    {
        var host = new FakeHost(FakeRegistration);
        var record = new KafkaRecord
        {
            Topic = "orders",
            Value = Encoding.UTF8.GetBytes("payload")
        };

        await FunctionsTestHostKafkaExtensions.InvokeKafkaAsync(host, "KafkaFunc", record, CancellationToken.None);

        var model = host.LastBindingData!.InputData[0].ModelBindingData;
        Assert.NotNull(model);
        Assert.Equal("AzureKafkaRecord", model!.Source);
        Assert.Equal("application/x-protobuf", model.ContentType);
        Assert.Equal("1.0", model.Version);
        Assert.NotEmpty(model.Content);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_Record_UsesCollectionModelBindingData()
    {
        var host = new FakeHost(FakeRegistration);
        var records = new[]
        {
            new KafkaRecord { Topic = "orders", Value = Encoding.UTF8.GetBytes("1") },
            new KafkaRecord { Topic = "orders", Value = Encoding.UTF8.GetBytes("2") }
        };

        await FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", records, CancellationToken.None);

        var collection = host.LastBindingData!.InputData[0].CollectionModelBindingData;
        Assert.NotNull(collection);
        Assert.Equal(2, collection!.Count);
        Assert.All(collection, item =>
        {
            Assert.Equal("AzureKafkaRecord", item.Source);
            Assert.Equal("application/x-protobuf", item.ContentType);
            Assert.NotEmpty(item.Content);
        });
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_Record_EmptyBatch_Throws()
    {
        var host = new FakeHost(FakeRegistration);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", Array.Empty<KafkaRecord>(), CancellationToken.None));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaAsync_Generic_UsesCamelCaseByDefault()
    {
        var host = new FakeHost(FakeRegistration);

        await FunctionsTestHostKafkaExtensions.InvokeKafkaAsync(host, "KafkaFunc", new SamplePayload { OrderId = "A1" }, cancellationToken: CancellationToken.None);

        using var doc = JsonDocument.Parse(host.LastBindingData!.InputData[0].Json!);
        Assert.Equal("A1", doc.RootElement.GetProperty("orderId").GetString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaAsync_Generic_UsesProvidedSerializerOptions()
    {
        var host = new FakeHost(FakeRegistration);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = null };

        await FunctionsTestHostKafkaExtensions.InvokeKafkaAsync(host, "KafkaFunc", new SamplePayload { OrderId = "A1" }, options, CancellationToken.None);

        using var doc = JsonDocument.Parse(host.LastBindingData!.InputData[0].Json!);
        Assert.Equal("A1", doc.RootElement.GetProperty("OrderId").GetString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_Generic_UsesJsonArray()
    {
        var host = new FakeHost(FakeRegistration);
        var payloads = new[]
        {
            new SamplePayload { OrderId = "A1" },
            new SamplePayload { OrderId = "B2" }
        };

        await FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", payloads, cancellationToken: CancellationToken.None);

        using var doc = JsonDocument.Parse(host.LastBindingData!.InputData[0].Json!);
        Assert.Equal("A1", doc.RootElement[0].GetProperty("orderId").GetString());
        Assert.Equal("B2", doc.RootElement[1].GetProperty("orderId").GetString());
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaBatchAsync_Generic_EmptyBatch_Throws()
    {
        var host = new FakeHost(FakeRegistration);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            FunctionsTestHostKafkaExtensions.InvokeKafkaBatchAsync(host, "KafkaFunc", Array.Empty<SamplePayload>(), cancellationToken: CancellationToken.None));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateBindingDataFromJson_MissingJson_UsesEmptyObject()
    {
        var context = new FunctionInvocationContext { TriggerType = "kafkaTrigger" };

        var binding = InvokeFactory("CreateBindingDataFromJson", context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateBindingDataFromBytes_MissingBytes_UsesEmptyArray()
    {
        var context = new FunctionInvocationContext { TriggerType = "kafkaTrigger" };

        var binding = InvokeFactory("CreateBindingDataFromBytes", context, FakeRegistration);

        Assert.Equal(Array.Empty<byte>(), binding.InputData[0].Bytes);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateBindingDataFromRecord_MissingRecord_Throws()
    {
        var context = new FunctionInvocationContext { TriggerType = "kafkaTrigger" };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeFactory("CreateBindingDataFromRecord", context, FakeRegistration));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateBindingDataFromRecords_MissingRecords_UsesEmptyCollection()
    {
        var context = new FunctionInvocationContext { TriggerType = "kafkaTrigger" };

        var binding = InvokeFactory("CreateBindingDataFromRecords", context, FakeRegistration);

        var collection = binding.InputData[0].CollectionModelBindingData;
        Assert.NotNull(collection);
        Assert.Empty(collection!);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaAsync_NullHost_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            FunctionsTestHostKafkaExtensions.InvokeKafkaAsync((IFunctionsTestHost)null!, "KafkaFunc", "hello", CancellationToken.None));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeKafkaAsync_EmptyFunctionName_Throws()
    {
        var host = new FakeHost(FakeRegistration);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            FunctionsTestHostKafkaExtensions.InvokeKafkaAsync(host, string.Empty, "hello", CancellationToken.None));
    }

    private static TriggerBindingData InvokeFactory(
        string methodName,
        FunctionInvocationContext context,
        FunctionRegistration registration)
    {
        var method = typeof(FunctionsTestHostKafkaExtensions)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [context, registration])!;
    }

    private sealed class FakeHost(FunctionRegistration registration) : IFunctionsTestHost
    {
        private readonly FakeInvoker _invoker = new(registration);

        public TriggerBindingData? LastBindingData => _invoker.LastBindingData;

        public IFunctionInvoker Invoker => _invoker;

        public IServiceProvider Services => new ServiceCollection().BuildServiceProvider();

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeInvoker(FunctionRegistration registration) : IFunctionInvoker
    {
        public TriggerBindingData? LastBindingData { get; private set; }

        public Task<FunctionInvocationResult> InvokeAsync(
            string functionName,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
            CancellationToken cancellationToken = default)
        {
            LastBindingData = triggerBindingFactory(context, registration);
            return Task.FromResult(new FunctionInvocationResult { Success = true });
        }

        public IReadOnlyDictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> GetFunctions()
            => new Dictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata>();
    }

    private sealed class SamplePayload
    {
        public required string OrderId { get; init; }
    }
}
