using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Warmup;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Warmup;

/// <summary>
/// Unit tests for the internal binding-data helper in
/// <see cref="FunctionsTestHostWarmupExtensions"/>.
/// </summary>
public class FunctionsTestHostWarmupExtensionsTests
{
    private static readonly FunctionRegistration FakeRegistration =
        new("fn-id-1", "WarmupFunc", "warmupTrigger", "warmupContext");

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateBindingData_DefaultWarmupContextJson_ProducesJsonParam()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "warmupTrigger",
            InputData = { ["$warmupContextJson"] = "{}" }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        var param = binding.InputData[0];
        Assert.Equal("warmupContext", param.Name);
        Assert.Equal("{}", param.Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateBindingData_MissingWarmupContextJson_UsesEmptyJson()
    {
        var context = new FunctionInvocationContext { TriggerType = "warmupTrigger" };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Single(binding.InputData);
        Assert.Equal("{}", binding.InputData[0].Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void CreateBindingData_NullWarmupContextJson_UsesEmptyJson()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "warmupTrigger",
            InputData = { ["$warmupContextJson"] = null! }
        };

        var binding = InvokeCreateBindingData(context, FakeRegistration);

        Assert.Equal("{}", binding.InputData[0].Json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void InvokeWarmupAsync_NullContext_UsesDefaultPayload()
    {
        var host = new FakeHost();
        _ = FunctionsTestHostWarmupExtensions.InvokeWarmupAsync(host, "WarmupFunc", null, TestContext.Current.CancellationToken);

        Assert.NotNull(host.LastContext);
        Assert.Equal("warmupTrigger", host.LastContext!.TriggerType);

        var json = host.LastContext.InputData["$warmupContextJson"]?.ToString();
        Assert.Equal("{}", json);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void InvokeWarmupAsync_WithContext_SerializesWarmupContext()
    {
        var host = new FakeHost();
        var warmupContext = new WarmupContext();
        _ = FunctionsTestHostWarmupExtensions.InvokeWarmupAsync(host, "WarmupFunc", warmupContext, TestContext.Current.CancellationToken);

        Assert.NotNull(host.LastContext);
        var json = host.LastContext!.InputData["$warmupContextJson"]?.ToString();
        Assert.NotNull(json);
        Assert.True(IsJsonObject(json!));
    }

    private static bool IsJsonObject(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object;
    }

    private static TriggerBindingData InvokeCreateBindingData(
        FunctionInvocationContext ctx, FunctionRegistration reg)
    {
        var method = typeof(FunctionsTestHostWarmupExtensions)
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
