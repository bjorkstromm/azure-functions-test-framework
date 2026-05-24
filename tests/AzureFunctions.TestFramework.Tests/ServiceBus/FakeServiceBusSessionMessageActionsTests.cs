using AzureFunctions.TestFramework.ServiceBus;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.ServiceBus;

/// <summary>
/// Represents this type.
/// </summary>
public class FakeServiceBusSessionMessageActionsTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task SessionMethods_RecordActions_AndPreserveState()
    {
        var sut = new FakeServiceBusSessionMessageActions();
        var state = BinaryData.FromString("abc");

        await sut.SetSessionStateAsync(state, TestContext.Current.CancellationToken);
        var readState = await sut.GetSessionStateAsync(TestContext.Current.CancellationToken);
        await sut.ReleaseSession(TestContext.Current.CancellationToken);
        await sut.RenewSessionLockAsync(TestContext.Current.CancellationToken);

        Assert.Equal(state, readState);
        Assert.Collection(
            sut.RecordedActions,
            x => Assert.Equal("SetSessionState", x.Action),
            x => Assert.Equal("GetSessionState", x.Action),
            x => Assert.Equal("ReleaseSession", x.Action),
            x => Assert.Equal("RenewSessionLock", x.Action));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task GetSessionStateAsync_DefaultsToEmptyBinaryData()
    {
        var sut = new FakeServiceBusSessionMessageActions();

        var state = await sut.GetSessionStateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(BinaryData.Empty, state);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Reset_ClearsRecordedActions()
    {
        var sut = new FakeServiceBusSessionMessageActions();
        sut.Reset();
        Assert.Empty(sut.RecordedActions);
    }
}
