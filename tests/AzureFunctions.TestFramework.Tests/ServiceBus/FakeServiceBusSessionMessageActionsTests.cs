using AzureFunctions.TestFramework.ServiceBus;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.ServiceBus;

public class FakeServiceBusSessionMessageActionsTests
{
    [Fact]
    public async Task SessionMethods_RecordActions_AndPreserveState()
    {
        var sut = new FakeServiceBusSessionMessageActions();
        var state = BinaryData.FromString("abc");

        await sut.SetSessionStateAsync(state);
        var readState = await sut.GetSessionStateAsync();
        await sut.ReleaseSession();
        await sut.RenewSessionLockAsync();

        Assert.Equal(state, readState);
        Assert.Collection(
            sut.RecordedActions,
            x => Assert.Equal("SetSessionState", x.Action),
            x => Assert.Equal("GetSessionState", x.Action),
            x => Assert.Equal("ReleaseSession", x.Action),
            x => Assert.Equal("RenewSessionLock", x.Action));
    }

    [Fact]
    public async Task GetSessionStateAsync_DefaultsToEmptyBinaryData()
    {
        var sut = new FakeServiceBusSessionMessageActions();

        var state = await sut.GetSessionStateAsync();

        Assert.Equal(BinaryData.Empty, state);
    }

    [Fact]
    public void Reset_ClearsRecordedActions()
    {
        var sut = new FakeServiceBusSessionMessageActions();
        sut.Reset();
        Assert.Empty(sut.RecordedActions);
    }
}
