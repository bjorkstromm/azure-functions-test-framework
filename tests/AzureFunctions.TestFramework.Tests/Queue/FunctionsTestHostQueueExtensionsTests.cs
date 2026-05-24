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

    // ── CreateBindingDataFromBytes ─────────────────────────────────────────────

    [Fact]
    public void CreateBindingDataFromBytes_WithBytes_UsesBytes()
    {
        var message = "hello queue";
        var bytes = Encoding.UTF8.GetBytes(message);

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { ["$queueMessageBytes"] = bytes }
        };

        var binding = InvokeCreateBindingDataFromBytes(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.Equal("myQueueItem", param.Name);
        Assert.NotNull(param.Bytes);
        Assert.Equal(bytes, param.Bytes);
    }

    [Fact]
    public void CreateBindingDataFromBytes_MissingBytes_UsesEmpty()
    {
        var context = new FunctionInvocationContext { TriggerType = "queueTrigger" };

        var binding = InvokeCreateBindingDataFromBytes(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal(Array.Empty<byte>(), binding.InputData[0].Bytes);
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

    // ── CreateBindingDataFromQueueMessage ──────────────────────────────────────

    [Fact]
    public void CreateBindingDataFromQueueMessage_ValidMessage_UsesModelBindingData()
    {
        var queueMessage = QueuesModelFactory.QueueMessage(
            messageId: "msg-3",
            popReceipt: "pop-3",
            messageText: "data",
            dequeueCount: 1);

        var context = new FunctionInvocationContext
        {
            TriggerType = "queueTrigger",
            InputData = { ["$queueMessage"] = queueMessage }
        };

        var binding = InvokeCreateBindingDataFromQueueMessage(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.Equal("myQueueItem", param.Name);
        Assert.NotNull(param.ModelBindingData);
        Assert.Equal("AzureStorageQueues", param.ModelBindingData!.Source);
    }

    [Fact]
    public void CreateBindingDataFromQueueMessage_MissingMessage_Throws()
    {
        var context = new FunctionInvocationContext { TriggerType = "queueTrigger" };

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            InvokeCreateBindingDataFromQueueMessage(context, FakeRegistration));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TriggerBindingData InvokeCreateBindingDataFromBytes(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostQueueExtensions)
            .GetMethod("CreateBindingDataFromBytes",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    private static TriggerBindingData InvokeCreateBindingDataFromQueueMessage(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostQueueExtensions)
            .GetMethod("CreateBindingDataFromQueueMessage",
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
}
