using System.Reflection;
using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.SignalR;

/// <summary>
/// Unit tests for the internal binding-data helper in
/// <see cref="FunctionsTestHostSignalRExtensions"/>.
/// </summary>
public class FunctionsTestHostSignalRExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "SignalRFunc", "signalRTrigger", "invocationContext");

    [Fact]
    public void CreateBindingData_WithInvocationJson_UsesJsonBinding()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "signalRTrigger",
            InputData = { ["$invocationContextJson"] = """{"connectionId":"conn-1"}""" }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal("invocationContext", binding.InputData[0].Name);
        Assert.Equal("""{"connectionId":"conn-1"}""", binding.InputData[0].Json);
    }

    [Fact]
    public void CreateBindingData_MissingInvocationJson_UsesEmptyJsonObject()
    {
        var context = new FunctionInvocationContext { TriggerType = "signalRTrigger" };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
    }

    [Fact]
    public void CreateBindingData_NullInvocationJson_UsesEmptyJsonObject()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "signalRTrigger",
            InputData = { ["$invocationContextJson"] = null! }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
    }

    [Fact]
    public async Task InvokeSignalRAsync_NullHost_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(
                null!,
                "SignalRFunc",
                new SignalRInvocationContext()));
    }

    [Fact]
    public async Task InvokeSignalRAsync_NullInvocationContext_Throws()
    {
        var host = new FakeHost();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(host, "SignalRFunc", null!));
    }

    [Fact]
    public void InvokeSignalRAsync_SerializesInvocationContextIntoTriggerInput()
    {
        var host = new FakeHost();
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-123",
            UserId = "user-456",
            Hub = "chat",
            Category = SignalRInvocationCategory.Messages,
            Event = "sendMessage",
            Arguments = ["hello"]
        };

        _ = host.InvokeSignalRAsync("SignalRFunc", invocationContext);

        Assert.NotNull(host.LastContext);
        Assert.Equal("signalRTrigger", host.LastContext!.TriggerType);

        var json = Assert.IsType<string>(host.LastContext.InputData["$invocationContextJson"]);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("conn-123", document.RootElement.GetProperty("connectionId").GetString());
        Assert.Equal("user-456", document.RootElement.GetProperty("userId").GetString());
        Assert.Equal("chat", document.RootElement.GetProperty("hub").GetString());
        Assert.Equal("sendMessage", document.RootElement.GetProperty("event").GetString());
        Assert.Equal("hello", document.RootElement.GetProperty("arguments")[0].GetString());
    }

    private static TriggerBindingData InvokeCreateBindingData(
        FunctionInvocationContext context,
        FunctionRegistration registration)
    {
        var method = typeof(FunctionsTestHostSignalRExtensions)
            .GetMethod("CreateBindingData", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [context, registration])!;
    }

    private sealed class FakeHost : IFunctionsTestHost
    {
        public FunctionInvocationContext? LastContext { get; private set; }

        public IFunctionInvoker Invoker => new FakeInvoker(this);
        public IServiceProvider Services => new ServiceCollection().BuildServiceProvider();
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class FakeInvoker(FakeHost host) : IFunctionInvoker
        {
            public Task<FunctionInvocationResult> InvokeAsync(
                string functionName,
                FunctionInvocationContext context,
                Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
                CancellationToken cancellationToken = default)
            {
                host.LastContext = context;
                return Task.FromResult(new FunctionInvocationResult { Success = true });
            }

            public IReadOnlyDictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> GetFunctions()
                => new Dictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata>();
        }
    }
}
