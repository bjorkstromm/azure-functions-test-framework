using AzureFunctions.TestFramework.Queue;
using AzureFunctions.TestFramework.SignalR;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace TestProject;

/// <summary>
/// Tests for SignalR trigger, input, and output bindings.
/// </summary>
public abstract class SignalRTestsBase : TestHostTestBase
{
    /// <summary>The fake SignalR endpoint URL injected for <c>[SignalRConnectionInfoInput]</c> tests.</summary>
    protected const string TestSignalRUrl = "https://test.signalr.net/client/?hub=chat&access_token=test";

    /// <summary>The fake access token injected for <c>[SignalRConnectionInfoInput]</c> tests.</summary>
    protected const string TestAccessToken = "test-access-token";

    private InMemoryProcessedItemsService? _processedItems;

    protected SignalRTestsBase(ITestOutputHelper output) : base(output) { }

    protected override async Task<IFunctionsTestHost> CreateTestHostAsync()
    {
        _processedItems = new InMemoryProcessedItemsService();
        return await CreateTestHostWithProcessedItemsAsync(_processedItems);
    }

    /// <summary>
    /// Creates the test host with the given <see cref="InMemoryProcessedItemsService"/>.
    /// Concrete implementations must register the processed items service and configure
    /// <see cref="FunctionsTestHostBuilderSignalRExtensions.WithSignalRConnectionInfo(IFunctionsTestHostBuilder, string, string)"/>
    /// with <see cref="TestSignalRUrl"/> / <see cref="TestAccessToken"/>.
    /// </summary>
    protected abstract Task<IFunctionsTestHost> CreateTestHostWithProcessedItemsAsync(
        InMemoryProcessedItemsService processedItems);

    // -------------------------------------------------------------------------
    // SignalR Trigger tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>[SignalRTrigger]</c> function is invoked and receives the
    /// <see cref="SignalRInvocationContext"/> correctly.
    /// </summary>
    [Fact]
    public async Task InvokeSignalRAsync_ProcessesMessage()
    {
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-abc",
            UserId = "user-1",
            Hub = "chat",
            Category = SignalRInvocationCategory.Messages,
            Event = "sendMessage",
            Arguments = ["Hello SignalR!"]
        };

        var result = await TestHost.InvokeSignalRAsync("ProcessSignalRMessage", invocationContext, TestCancellation);

        Assert.True(result.Success, $"SignalR invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("conn-abc:sendMessage", processed[0]);
    }

    /// <summary>
    /// Verifies that a <c>[SignalRTrigger]</c> function correctly receives connection events.
    /// </summary>
    [Fact]
    public async Task InvokeSignalRAsync_ProcessesConnectionEvent()
    {
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-xyz",
            UserId = "user-2",
            Hub = "chat",
            Category = SignalRInvocationCategory.Connections,
            Event = "connected"
        };

        var result = await TestHost.InvokeSignalRAsync("ProcessSignalRConnection", invocationContext, TestCancellation);

        Assert.True(result.Success, $"SignalR connection event invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("connected:conn-xyz", processed[0]);
    }

    // -------------------------------------------------------------------------
    // SignalR Output Binding tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <c>[SignalRTrigger]</c> function with a <c>[SignalROutput]</c> return value
    /// captures the returned <see cref="SignalRMessageAction"/>.
    /// </summary>
    [Fact]
    public async Task InvokeSignalRAsync_WithOutputBinding_CapturesReturnValue()
    {
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-out",
            Hub = "chat",
            Category = SignalRInvocationCategory.Messages,
            Event = "broadcast",
            Arguments = ["Hello World"]
        };

        var result = await TestHost.InvokeSignalRAsync("BroadcastSignalRMessage", invocationContext, TestCancellation);

        Assert.True(result.Success, $"SignalR output binding invocation failed: {result.Error}");

        // Verify trigger side-effect
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal("conn-out", processed[0]);

        // Verify output binding return value via JsonElement (SignalRMessageAction has multiple
        // parameterized constructors so ReadReturnValueAs<SignalRMessageAction> is not available;
        // verify through the JSON representation instead).
        var actionJson = result.ReadReturnValueAs<System.Text.Json.JsonElement>();
        Assert.Equal("broadcast", actionJson.GetProperty("Target").GetString());
        var args = actionJson.GetProperty("Arguments");
        Assert.Equal(1, args.GetArrayLength());
        Assert.Equal("echo:Hello World", args[0].GetString());
    }

    // -------------------------------------------------------------------------
    // SignalRConnectionInfo Input Binding tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a queue-triggered function with a <c>[SignalRConnectionInfoInput]</c> binding
    /// receives the URL registered via <c>WithSignalRConnectionInfo</c> in the builder.
    /// </summary>
    [Fact]
    public async Task InvokeQueueAsync_WithSignalRConnectionInfoInput_ReadsRegisteredUrl()
    {
        var result = await TestHost.InvokeQueueAsync("ReadSignalRConnectionInfo", "unused", TestCancellation);

        Assert.True(result.Success, $"SignalR connection info input invocation failed: {result.Error}");
        var processed = _processedItems!.TakeAll();
        Assert.Single(processed);
        Assert.Equal(TestSignalRUrl, processed[0]);
    }
}
