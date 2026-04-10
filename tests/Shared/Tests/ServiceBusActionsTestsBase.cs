using Azure.Messaging.ServiceBus;
using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TestProject;

/// <summary>
/// Shared abstract test base for verifying <see cref="ServiceBusMessageActions"/> and
/// <see cref="ServiceBusSessionMessageActions"/> fake injection across all four host flavours.
/// </summary>
public abstract class ServiceBusActionsTestsBase : TestHostTestBase
{
    private InMemoryProcessedItemsService? _processedItems;

    protected ServiceBusActionsTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithServicesAsync(_processedItems);
    }

    /// <summary>
    /// Creates a test host that includes both the <see cref="InMemoryProcessedItemsService"/>
    /// and the fake Service Bus message-actions support.
    /// </summary>
    protected abstract Task<IFunctionsTestHost> CreateTestHostWithServicesAsync(
        InMemoryProcessedItemsService processedItems);

    [Fact]
    public async Task InvokeServiceBusAsync_WithMessageActions_CompletesMessage()
    {
        var body = "Message to complete";
        var receivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: Guid.NewGuid().ToString());

        var result = await TestHost.InvokeServiceBusAsync(
            "ProcessServiceBusMessageWithActions", receivedMessage, TestCancellation);

        Assert.True(result.Success, $"Invocation failed: {result.Error}");

        // Verify the function processed the message
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(body, processed[0]);

        // Verify CompleteMessageAsync was called via the fake
        var fakeActions = TestHost.Services.GetRequiredService<FakeServiceBusMessageActions>();
        Assert.Single(fakeActions.RecordedActions);
        var action = fakeActions.RecordedActions[0];
        Assert.Equal("Complete", action.Action);
        Assert.Equal(receivedMessage.MessageId, action.Message.MessageId);
    }
}
