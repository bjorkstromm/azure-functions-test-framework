using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Queue;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Queue;

/// <summary>
/// Unit tests for the internal binding-data helpers in
/// <see cref="FunctionsTestHostQueueExtensions"/>.
/// </summary>
public class FunctionsTestHostQueueExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "QueueFunc", "queueTrigger", "myQueueItem");

    // ── Queue payload binding ──────────────────────────────────────────────────

    [Fact]
    public async Task InvokeQueueAsync_WithBytes_UsesBytes()
    {
        var bytes = Encoding.UTF8.GetBytes("hello queue");
        var host = new FakeHost();

        await host.InvokeQueueAsync("QueueFunc", bytes, TestContext.Current.CancellationToken);

        var param = Assert.Single(host.FakeInvoker.BindingData!.InputData);
        Assert.Equal("myQueueItem", param.Name);
        Assert.Equal(bytes, param.Bytes);
    }

    [Fact]
    public async Task InvokeQueueAsync_WithString_UsesUtf8Bytes()
    {
        var host = new FakeHost();

        await host.InvokeQueueAsync("QueueFunc", "hello queue", TestContext.Current.CancellationToken);

        var param = Assert.Single(host.FakeInvoker.BindingData!.InputData);
        Assert.Equal(Encoding.UTF8.GetBytes("hello queue"), param.Bytes);
    }

    [Fact]
    public async Task InvokeQueueAsync_WithPoco_UsesCamelCaseJson()
    {
        var host = new FakeHost();

        await host.InvokeQueueAsync(
            "QueueFunc",
            new TestQueuePayload { OrderId = "order-42" },
            jsonSerializerOptions: null,
            cancellationToken: TestContext.Current.CancellationToken);

        var param = Assert.Single(host.FakeInvoker.BindingData!.InputData);
        Assert.Equal("myQueueItem", param.Name);
        Assert.Equal("""{"orderId":"order-42"}""", param.Json);
    }

    [Fact]
    public void SerializePayloadToJson_UsesCamelCaseByDefault()
    {
        var payload = new TestQueuePayload { OrderId = "order-123" };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var json = InvokeSerializePayloadToJson(payload, typeof(TestQueuePayload), options);

        Assert.Equal("""{"orderId":"order-123"}""", json);
    }

    [Fact]
    public void SerializePayloadToJson_WhenSerializationFails_ThrowsInvalidOperationException()
    {
        var payload = new CyclicPayload();
        payload.Self = payload;
        var options = new JsonSerializerOptions();

        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeSerializePayloadToJson(payload, typeof(CyclicPayload), options));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // ── SerializeQueueMessage ────────────────────────────────────────────────

    [Fact]
    public void SerializeQueueMessage_AllOptionalFields_IncludesAllFields()
    {
        var queueMessage = QueuesModelFactory.QueueMessage(
            messageId: "msg-1",
            popReceipt: "pop-1",
            messageText: "hello",
            dequeueCount: 3,
            nextVisibleOn: DateTimeOffset.UtcNow.AddMinutes(30),
            insertedOn: DateTimeOffset.UtcNow.AddMinutes(-10),
            expiresOn: DateTimeOffset.UtcNow.AddDays(7));

        var bytes = InvokeSerializeQueueMessage(queueMessage);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("msg-1", doc.RootElement.GetProperty("MessageId").GetString());
        Assert.Equal("pop-1", doc.RootElement.GetProperty("PopReceipt").GetString());
        Assert.Equal("hello", doc.RootElement.GetProperty("MessageText").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("DequeueCount").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("NextVisibleOn", out _));
        Assert.True(doc.RootElement.TryGetProperty("InsertedOn", out _));
        Assert.True(doc.RootElement.TryGetProperty("ExpiresOn", out _));
    }

    [Fact]
    public void SerializeQueueMessage_NoOptionalDates_OmitsOptionalFields()
    {
        var queueMessage = QueuesModelFactory.QueueMessage(
            messageId: "msg-2",
            popReceipt: "pop-2",
            messageText: "test",
            dequeueCount: 1,
            nextVisibleOn: null,
            insertedOn: null,
            expiresOn: null);

        var bytes = InvokeSerializeQueueMessage(queueMessage);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);

        Assert.False(doc.RootElement.TryGetProperty("NextVisibleOn", out _));
        Assert.False(doc.RootElement.TryGetProperty("InsertedOn", out _));
        Assert.False(doc.RootElement.TryGetProperty("ExpiresOn", out _));
    }

    [Fact]
    public async Task InvokeQueueAsync_WithQueueMessage_UsesModelBindingData()
    {
        var queueMessage = QueuesModelFactory.QueueMessage(
            messageId: "msg-3",
            popReceipt: "pop-3",
            messageText: "data",
            dequeueCount: 1);
        var host = new FakeHost();

        await host.InvokeQueueAsync("QueueFunc", queueMessage, TestContext.Current.CancellationToken);

        var param = Assert.Single(host.FakeInvoker.BindingData!.InputData);
        Assert.Equal("myQueueItem", param.Name);
        Assert.NotNull(param.ModelBindingData);
        Assert.Equal("AzureStorageQueues", param.ModelBindingData!.Source);
    }

    [Fact]
    public void CreateBindingData_MissingPayload_Throws()
    {
        var context = new FunctionInvocationContext { TriggerType = "queueTrigger" };

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            InvokeCreateBindingData(context, FakeRegistration));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TriggerBindingData InvokeCreateBindingData(FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostQueueExtensions)
            .GetMethod("CreateBindingData",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    private static byte[] InvokeSerializeQueueMessage(QueueMessage message)
    {
        var method = typeof(FunctionsTestHostQueueExtensions)
            .GetMethod("SerializeQueueMessage",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (byte[])method.Invoke(null, [message])!;
    }

    private static string InvokeSerializePayloadToJson(object payload, Type payloadType, JsonSerializerOptions options)
    {
        var method = typeof(FunctionsTestHostQueueExtensions)
            .GetMethod("SerializePayloadToJson",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [payload, payloadType, options])!;
    }

    private sealed class TestQueuePayload
    {
        public string OrderId { get; set; } = string.Empty;
    }

    private sealed class CyclicPayload
    {
        public CyclicPayload? Self { get; set; }
    }

    private sealed class FakeHost : IFunctionsTestHost
    {
        public FakeInvoker FakeInvoker { get; } = new();
        public IServiceProvider Services => throw new NotSupportedException();
        public IFunctionInvoker Invoker => FakeInvoker;

        public Task StartAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task StopAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { }
    }

    private sealed class FakeInvoker : IFunctionInvoker
    {
        public TriggerBindingData? BindingData { get; private set; }

        public Task<FunctionInvocationResult> InvokeAsync(
            string functionName,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
            CancellationToken cancellationToken = default)
        {
            BindingData = triggerBindingFactory(context, FakeRegistration);
            return Task.FromResult(new FunctionInvocationResult());
        }

        public IReadOnlyDictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata>
            GetFunctions() => throw new NotSupportedException();
    }
}
