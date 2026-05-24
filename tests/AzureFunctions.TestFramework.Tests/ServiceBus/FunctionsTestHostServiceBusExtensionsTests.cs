using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.ServiceBus;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Reflection;
using System.Text;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.ServiceBus;

/// <summary>
/// Unit tests for the internal binding-data helpers in
/// <see cref="FunctionsTestHostServiceBusExtensions"/>.
/// </summary>
public class FunctionsTestHostServiceBusExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "ServiceBusFunc", "serviceBusTrigger", "myMessage");

    // ── CreateBindingDataFromBytes ─────────────────────────────────────────────

    [Fact]
    public void CreateBindingDataFromBytes_WithBytes_UsesBytesAndMetadata()
    {
        var bytes = Encoding.UTF8.GetBytes("hello service bus");
        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData =
            {
                ["$messageBodyBytes"] = bytes,
                ["$triggerMetadata"] = """{"messageId":"msg-1"}"""
            }
        };

        var binding = InvokeCreateBindingDataFromBytes(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal("myMessage", binding.InputData[0].Name);
        Assert.Equal(bytes, binding.InputData[0].Bytes);
        Assert.NotNull(binding.TriggerMetadataJson);
        Assert.Equal("""{"messageId":"msg-1"}""", binding.TriggerMetadataJson!["myMessage"]);
    }

    [Fact]
    public void CreateBindingDataFromBytes_MissingBytes_UsesEmptyArray()
    {
        var context = new FunctionInvocationContext { TriggerType = "serviceBusTrigger" };

        var binding = InvokeCreateBindingDataFromBytes(context, FakeRegistration);

        Assert.Equal(Array.Empty<byte>(), binding.InputData[0].Bytes);
    }

    [Fact]
    public void CreateBindingDataFromBytes_NoTriggerMetadata_NullMetadata()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData = { ["$messageBodyBytes"] = Array.Empty<byte>() }
        };

        var binding = InvokeCreateBindingDataFromBytes(context, FakeRegistration);

        Assert.Null(binding.TriggerMetadataJson);
    }

    // ── CreateBindingDataFromReceivedMessages — single message ────────────────

    [Fact]
    public void CreateBindingDataFromReceivedMessages_SingleMessage_UsesModelBindingData()
    {
        var sbMessage = new ServiceBusMessage(BinaryData.FromString("test body"));
        var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: sbMessage.Body,
            messageId: "msg-2");

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData = { ["$receivedMessages"] = new[] { receivedMessage } }
        };

        var binding = InvokeCreateBindingDataFromReceivedMessages(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.Equal("myMessage", param.Name);
        Assert.NotNull(param.ModelBindingData);
        Assert.Equal("AzureServiceBusReceivedMessage", param.ModelBindingData!.Source);
        Assert.Equal("application/octet-stream", param.ModelBindingData.ContentType);
    }

    // ── CreateBindingDataFromReceivedMessages — batch ─────────────────────────

    [Fact]
    public void CreateBindingDataFromReceivedMessages_BatchMessages_UsesCollectionModelBindingData()
    {
        var msg1 = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("msg1"), messageId: "id-1");
        var msg2 = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("msg2"), messageId: "id-2");

        var context = new FunctionInvocationContext
        {
            TriggerType = "serviceBusTrigger",
            InputData = { ["$receivedMessages"] = new[] { msg1, msg2 } }
        };

        var binding = InvokeCreateBindingDataFromReceivedMessages(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.NotNull(param.CollectionModelBindingData);
        Assert.Equal(2, param.CollectionModelBindingData!.Count);
    }

    // ── InvokeServiceBusAsync (from ServiceBusMessage) validation ─────────────

    [Fact]
    public async Task InvokeServiceBusAsync_ServiceBusMessage_NullMessage_Throws()
    {
        var host = new FakeHost();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => FunctionsTestHostServiceBusExtensions.InvokeServiceBusAsync(host, "Func", (ServiceBusMessage)null!));
    }

    // ── InvokeServiceBusBatchAsync validation ──────────────────────────────────

    [Fact]
    public async Task InvokeServiceBusBatchAsync_EmptyBatch_Throws()
    {
        var host = new FakeHost();
        await Assert.ThrowsAsync<ArgumentException>(
            () => FunctionsTestHostServiceBusExtensions.InvokeServiceBusBatchAsync(host, "Func", []));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TriggerBindingData InvokeCreateBindingDataFromBytes(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostServiceBusExtensions)
            .GetMethod("CreateBindingDataFromBytes",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    private static TriggerBindingData InvokeCreateBindingDataFromReceivedMessages(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostServiceBusExtensions)
            .GetMethod("CreateBindingDataFromReceivedMessages",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    private sealed class FakeHost : IFunctionsTestHost
    {
        public IFunctionInvoker Invoker => new FakeInvoker();
        public IServiceProvider Services => new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class FakeInvoker : IFunctionInvoker
        {
            public Task<FunctionInvocationResult> InvokeAsync(
                string functionName,
                FunctionInvocationContext context,
                Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
                CancellationToken cancellationToken = default)
                => Task.FromResult(new FunctionInvocationResult { Success = true });

            public IReadOnlyDictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> GetFunctions()
                => new Dictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata>();
        }
    }
}
