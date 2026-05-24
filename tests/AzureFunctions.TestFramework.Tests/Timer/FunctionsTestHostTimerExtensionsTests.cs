using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Timer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Timer;

/// <summary>
/// Unit tests for the internal binding-data helper in
/// <see cref="FunctionsTestHostTimerExtensions"/>.
/// </summary>
public class FunctionsTestHostTimerExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "TimerFunc", "timerTrigger", "myTimer");

    // ── CreateBindingData ─────────────────────────────────────────────────────

    [Fact]
    public void CreateBindingData_DefaultTimerJson_ProducesJsonParam()
    {
        var json = JsonSerializer.Serialize(new TimerInfo(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var context = new FunctionInvocationContext
        {
            TriggerType = "timerTrigger",
            InputData = { ["$timerJson"] = json }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.Equal("myTimer", param.Name);
        Assert.NotNull(param.Json);
        Assert.False(string.IsNullOrWhiteSpace(param.Json));
    }

    [Fact]
    public void CreateBindingData_MissingTimerJson_UsesEmptyJson()
    {
        var context = new FunctionInvocationContext { TriggerType = "timerTrigger" };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal("{}", binding.InputData[0].Json);
    }

    [Fact]
    public void CreateBindingData_NullTimerJson_UsesEmptyJson()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "timerTrigger",
            InputData = { ["$timerJson"] = null! }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
    }

    // ── InvokeTimerAsync passes default TimerInfo when null ───────────────────

    [Fact]
    public void InvokeTimerAsync_NullTimerInfo_CreatesDefaultTimerInfo()
    {
        // Verify the JSON produced by null timerInfo is valid and contains isPastDue
        var host = new FakeHost();
        _ = FunctionsTestHostTimerExtensions.InvokeTimerAsync(host, "TimerFunc", null);

        Assert.NotNull(host.LastContext);
        var json = host.LastContext.InputData["$timerJson"]?.ToString();
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        Assert.True(doc.RootElement.TryGetProperty("isPastDue", out _));
    }

    [Fact]
    public void InvokeTimerAsync_WithTimerInfo_SerializesTimerInfo()
    {
        var timerInfo = new TimerInfo { IsPastDue = true };
        var host = new FakeHost();
        _ = FunctionsTestHostTimerExtensions.InvokeTimerAsync(host, "TimerFunc", timerInfo);

        Assert.NotNull(host.LastContext);
        var json = host.LastContext.InputData["$timerJson"]?.ToString();
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        Assert.True(doc.RootElement.GetProperty("isPastDue").GetBoolean());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TriggerBindingData InvokeCreateBindingData(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostTimerExtensions)
            .GetMethod("CreateBindingData",
                BindingFlags.NonPublic | BindingFlags.Static)!;
        return (TriggerBindingData)method.Invoke(null, [ctx, reg])!;
    }

    /// <summary>
    /// A minimal fake host that captures the last InvokeAsync call context.
    /// </summary>
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
