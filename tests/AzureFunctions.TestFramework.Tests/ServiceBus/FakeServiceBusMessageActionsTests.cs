using Azure.Messaging.ServiceBus;
using AzureFunctions.TestFramework.ServiceBus;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.ServiceBus;

/// <summary>
/// Represents this type.
/// </summary>
public class FakeServiceBusMessageActionsTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task SettlementMethods_RecordActions()
    {
        var sut = new FakeServiceBusMessageActions();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "id1", body: BinaryData.FromString("payload"));

        await sut.CompleteMessageAsync(message, TestContext.Current.CancellationToken);
        await sut.AbandonMessageAsync(message, new Dictionary<string, object> { ["k"] = 1 }, TestContext.Current.CancellationToken);
        await sut.DeadLetterMessageAsync(message, deadLetterReason: "r", deadLetterErrorDescription: "d", cancellationToken: TestContext.Current.CancellationToken);
        await sut.DeferMessageAsync(message, cancellationToken: TestContext.Current.CancellationToken);
        await sut.RenewMessageLockAsync(message, TestContext.Current.CancellationToken);

        Assert.Collection(
            sut.RecordedActions,
            x => Assert.Equal("Complete", x.Action),
            x => Assert.Equal("Abandon", x.Action),
            x => Assert.Equal("DeadLetter", x.Action),
            x => Assert.Equal("Defer", x.Action),
            x => Assert.Equal("RenewLock", x.Action));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task SettlementMethods_NullMessage_Throw()
    {
        var sut = new FakeServiceBusMessageActions();

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CompleteMessageAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AbandonMessageAsync(null!, propertiesToModify: null, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeadLetterMessageAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeferMessageAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RenewMessageLockAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task Reset_ClearsRecordedActions()
    {
        var sut = new FakeServiceBusMessageActions();
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "id1", body: BinaryData.FromString("payload"));

        await sut.CompleteMessageAsync(message, TestContext.Current.CancellationToken);
        sut.Reset();

        Assert.Empty(sut.RecordedActions);
    }
}
