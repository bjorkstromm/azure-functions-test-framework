using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.SignalR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.SignalR;

/// <summary>
/// Unit tests for <see cref="FunctionsTestHostSignalRExtensions"/>.
/// </summary>
public class FunctionsTestHostSignalRExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "SignalRFunc", "signalRTrigger", "invocationContext");

    // ── InvokeSignalRAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeSignalRAsync_NullHost_Throws()
    {
        var context = new SignalRInvocationContext { ConnectionId = "c1", Hub = "chat" };
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(null!, "SignalRFunc", context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvokeSignalRAsync_NullContext_Throws()
    {
        var host = new FakeHost();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(host, "SignalRFunc", null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InvokeSignalRAsync_SetsCorrectTriggerType()
    {
        var host = new FakeHost();
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-1",
            Hub = "chat",
            Category = SignalRInvocationCategory.Messages,
            Event = "sendMessage"
        };

        _ = FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(host, "SignalRFunc", invocationContext, TestContext.Current.CancellationToken);

        Assert.Equal("signalRTrigger", host.LastContext!.TriggerType);
    }

    [Fact]
    public void InvokeSignalRAsync_SerializesInvocationContextToJson()
    {
        var host = new FakeHost();
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-abc",
            UserId = "user-1",
            Hub = "chat",
            Category = SignalRInvocationCategory.Messages,
            Event = "sendMessage"
        };

        _ = FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(host, "SignalRFunc", invocationContext, TestContext.Current.CancellationToken);

        Assert.NotNull(host.LastContext);
        Assert.True(host.LastContext!.InputData.ContainsKey("$invocationContextJson"));

        var json = host.LastContext.InputData["$invocationContextJson"]!.ToString()!;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("conn-abc", doc.RootElement.GetProperty("connectionId").GetString());
        Assert.Equal("user-1", doc.RootElement.GetProperty("userId").GetString());
        Assert.Equal("chat", doc.RootElement.GetProperty("hub").GetString());
        Assert.Equal("sendMessage", doc.RootElement.GetProperty("event").GetString());
    }

    [Fact]
    public void InvokeSignalRAsync_ConnectionEvent_SerializesCategory()
    {
        var host = new FakeHost();
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-xyz",
            Hub = "chat",
            Category = SignalRInvocationCategory.Connections,
            Event = "connected"
        };

        _ = FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(host, "SignalRFunc", invocationContext, TestContext.Current.CancellationToken);

        var json = host.LastContext!.InputData["$invocationContextJson"]!.ToString()!;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("connected", doc.RootElement.GetProperty("event").GetString());
    }

    [Fact]
    public void InvokeSignalRAsync_WithArguments_SerializesArguments()
    {
        var host = new FakeHost();
        var invocationContext = new SignalRInvocationContext
        {
            ConnectionId = "conn-1",
            Hub = "chat",
            Category = SignalRInvocationCategory.Messages,
            Event = "broadcast",
            Arguments = ["Hello World", "second arg"]
        };

        _ = FunctionsTestHostSignalRExtensions.InvokeSignalRAsync(host, "SignalRFunc", invocationContext, TestContext.Current.CancellationToken);

        var json = host.LastContext!.InputData["$invocationContextJson"]!.ToString()!;
        using var doc = JsonDocument.Parse(json);
        var args = doc.RootElement.GetProperty("arguments");
        Assert.Equal(2, args.GetArrayLength());
        Assert.Equal("Hello World", args[0].GetString());
        Assert.Equal("second arg", args[1].GetString());
    }

    // ── CreateBindingData (internal) ──────────────────────────────────────────

    [Fact]
    public void CreateBindingData_WithContextJson_ProducesJsonParam()
    {
        var contextJson = """{"connectionId":"c1","hub":"chat","event":"sendMessage"}""";
        var context = new FunctionInvocationContext
        {
            TriggerType = "signalRTrigger",
            InputData = { ["$invocationContextJson"] = contextJson }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.Equal("invocationContext", param.Name);
        Assert.NotNull(param.Json);
        Assert.Contains("c1", param.Json!);
    }

    [Fact]
    public void CreateBindingData_MissingContextJson_UsesEmptyObject()
    {
        var context = new FunctionInvocationContext { TriggerType = "signalRTrigger" };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal("{}", binding.InputData[0].Json);
    }

    [Fact]
    public void CreateBindingData_NullContextJsonValue_UsesEmptyObject()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "signalRTrigger",
            InputData = { ["$invocationContextJson"] = null! }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TriggerBindingData InvokeCreateBindingData(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostSignalRExtensions)
            .GetMethod("CreateBindingData",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
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

        private sealed class FakeInvoker : IFunctionInvoker
        {
            private readonly FakeHost _host;
            public FakeInvoker(FakeHost host) => _host = host;

            public Task<FunctionInvocationResult> InvokeAsync(
                string functionName,
                FunctionInvocationContext context,
                Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
                CancellationToken cancellationToken = default)
            {
                _host.LastContext = context;
                return Task.FromResult(new FunctionInvocationResult { Success = true });
            }

            public IReadOnlyDictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata> GetFunctions()
                => new Dictionary<string, Microsoft.Azure.Functions.Worker.Core.FunctionMetadata.IFunctionMetadata>();
        }
    }
}
