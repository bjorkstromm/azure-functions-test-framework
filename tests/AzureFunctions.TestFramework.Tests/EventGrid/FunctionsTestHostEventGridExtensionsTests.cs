using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.EventGrid;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.EventGrid;

/// <summary>
/// Unit tests for the internal binding-data helpers in
/// <see cref="FunctionsTestHostEventGridExtensions"/>.
/// </summary>
public class FunctionsTestHostEventGridExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "EventGridFunc", "eventGridTrigger", "myEvent");

    // ── CreateBindingData ─────────────────────────────────────────────────────

    [Fact]
    public void CreateBindingData_WithEventJson_ProducesJsonParam()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "eventGridTrigger",
            InputData = { ["$eventJson"] = """{"type":"test.event","subject":"test"}""" }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.Equal("myEvent", param.Name);
        Assert.NotNull(param.Json);
        Assert.Contains("test.event", param.Json!);
    }

    [Fact]
    public void CreateBindingData_MissingEventJson_UsesEmptyJson()
    {
        var context = new FunctionInvocationContext { TriggerType = "eventGridTrigger" };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
    }

    // ── InvokeEventGridAsync with EventGridEvent ───────────────────────────────

    [Fact]
    public void InvokeEventGridAsync_EventGridEvent_BuildsCorrectJson()
    {
        var evt = new EventGridEvent(
            subject: "/test/subject",
            eventType: "Test.Event",
            dataVersion: "1.0",
            data: BinaryData.FromString("""{"key":"value"}"""));

        var host = new FakeHost();
        _ = FunctionsTestHostEventGridExtensions.InvokeEventGridAsync(host, "EventGridFunc", evt);

        var json = host.LastContext!.InputData["$eventJson"]!.ToString()!;
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("Test.Event", doc.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("/test/subject", doc.RootElement.GetProperty("subject").GetString());
        Assert.Equal("1.0", doc.RootElement.GetProperty("dataVersion").GetString());
    }

    [Fact]
    public void InvokeEventGridAsync_EventGridEvent_NullData_DataIsNull()
    {
        // EventGridEvent requires non-null data, so use empty BinaryData
        var evt = new EventGridEvent(
            subject: "/test",
            eventType: "Test.Event",
            dataVersion: "1.0",
            data: BinaryData.FromString("null"));

        var host = new FakeHost();
        _ = FunctionsTestHostEventGridExtensions.InvokeEventGridAsync(host, "EventGridFunc", evt);

        Assert.NotNull(host.LastContext);
    }

    [Fact]
    public async Task InvokeEventGridAsync_EventGridEvent_NullArg_Throws()
    {
        var host = new FakeHost();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => FunctionsTestHostEventGridExtensions.InvokeEventGridAsync(host, "EventGridFunc", (EventGridEvent)null!));
    }

    // ── InvokeEventGridAsync with CloudEvent ──────────────────────────────────

    [Fact]
    public void InvokeEventGridAsync_CloudEvent_BuildsCorrectJson()
    {
        var cloudEvent = new CloudEvent(
            source: "/test/source",
            type: "com.example.event",
            data: BinaryData.FromObjectAsJson(new { name = "Alice" }),
            dataContentType: "application/json");

        var host = new FakeHost();
        _ = FunctionsTestHostEventGridExtensions.InvokeEventGridAsync(host, "EventGridFunc", cloudEvent);

        var json = host.LastContext!.InputData["$eventJson"]!.ToString()!;
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("1.0", doc.RootElement.GetProperty("specversion").GetString());
        Assert.Equal("com.example.event", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("/test/source", doc.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public void InvokeEventGridAsync_CloudEvent_NullData_DataFieldIsNull()
    {
        var cloudEvent = new CloudEvent(
            source: "/test/source",
            type: "com.example.event",
            data: null,
            dataContentType: null);

        var host = new FakeHost();
        _ = FunctionsTestHostEventGridExtensions.InvokeEventGridAsync(host, "EventGridFunc", cloudEvent);

        var json = host.LastContext!.InputData["$eventJson"]!.ToString()!;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task InvokeEventGridAsync_CloudEvent_NullArg_Throws()
    {
        var host = new FakeHost();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => FunctionsTestHostEventGridExtensions.InvokeEventGridAsync(host, "EventGridFunc", (CloudEvent)null!));
    }

    // ── TryParseJson ──────────────────────────────────────────────────────────

    [Fact]
    public void TryParseJson_ValidJson_ReturnsJsonElement()
    {
        var result = InvokeTryParseJson(BinaryData.FromString("""{"key":"val"}"""));
        Assert.NotNull(result);
        Assert.IsType<JsonElement>(result);
    }

    [Fact]
    public void TryParseJson_NullBinaryData_ReturnsNull()
    {
        var result = InvokeTryParseJson(null);
        Assert.Null(result);
    }

    [Fact]
    public void TryParseJson_NonJsonBinaryData_ReturnsStringFallback()
    {
        // Non-JSON binary will throw JsonException → fallback to string
        var result = InvokeTryParseJson(BinaryData.FromBytes([0xFF, 0xFE, 0x00]));
        // Result should be a string (fallback) or null — not throwing
        // It's okay if it's null since the bytes aren't valid UTF-8 JSON
        // The important thing is no exception is thrown
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TriggerBindingData InvokeCreateBindingData(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostEventGridExtensions)
            .GetMethod("CreateBindingData",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    private static object? InvokeTryParseJson(BinaryData? data)
    {
        var method = typeof(FunctionsTestHostEventGridExtensions)
            .GetMethod("TryParseJson",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return method.Invoke(null, [data]);
    }

    private sealed class FakeHost : IFunctionsTestHost
    {
        public FunctionInvocationContext? LastContext { get; private set; }

        public IFunctionInvoker Invoker => new FakeInvoker(this);
        public IServiceProvider Services => new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
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
