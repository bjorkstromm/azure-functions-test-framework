using Azure.Messaging.ServiceBus;
using AzureFunctions.TestFramework.ServiceBus;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.ServiceBus;

public class FakeServiceBusMessageActionsTests
{
    [Fact]
    public async Task SettlementMethods_RecordActions()
    {
        var sut = new FakeServiceBusMessageActions();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "id1", body: BinaryData.FromString("payload"));

        await sut.CompleteMessageAsync(message);
        await sut.AbandonMessageAsync(message, new Dictionary<string, object> { ["k"] = 1 });
        await sut.DeadLetterMessageAsync(message, deadLetterReason: "r", deadLetterErrorDescription: "d");
        await sut.DeferMessageAsync(message);
        await sut.RenewMessageLockAsync(message);

        Assert.Collection(
            sut.RecordedActions,
            x => Assert.Equal("Complete", x.Action),
            x => Assert.Equal("Abandon", x.Action),
            x => Assert.Equal("DeadLetter", x.Action),
            x => Assert.Equal("Defer", x.Action),
            x => Assert.Equal("RenewLock", x.Action));
    }

    [Fact]
    public async Task SettlementMethods_NullMessage_Throw()
    {
        var sut = new FakeServiceBusMessageActions();

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CompleteMessageAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AbandonMessageAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeadLetterMessageAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeferMessageAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RenewMessageLockAsync(null!));
    }

    [Fact]
    public async Task Reset_ClearsRecordedActions()
    {
        var sut = new FakeServiceBusMessageActions();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "id1", body: BinaryData.FromString("payload"));

        await sut.CompleteMessageAsync(message);
        sut.Reset();

        Assert.Empty(sut.RecordedActions);
    }
}
