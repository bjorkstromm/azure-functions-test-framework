using System.Reflection;
using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using WorkerRetryContext = Microsoft.Azure.Functions.Worker.RetryContext;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableOrchestrationRunner.InvokeMethodAsync"/>
/// and <see cref="FakeDurableOrchestrationRunner.BuildArguments"/>.
/// </summary>
public class FakeDurableOrchestrationRunnerInvokeMethodTests
{
    // -------------------------------------------------------------------------
    // InvokeMethodAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InvokeMethodAsync_SyncVoidMethod_ReturnsNull()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.VoidMethod))!;
        var result = await FakeDurableOrchestrationRunner.InvokeMethodAsync(method, null, Array.Empty<object?>());
        Assert.Null(result);
    }

    [Fact]
    public async Task InvokeMethodAsync_SyncReturnMethod_ReturnsValue()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.ReturnInt))!;
        var result = await FakeDurableOrchestrationRunner.InvokeMethodAsync(method, null, Array.Empty<object?>());
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task InvokeMethodAsync_AsyncTaskMethod_ReturnsNull()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.AsyncTask))!;
        var result = await FakeDurableOrchestrationRunner.InvokeMethodAsync(method, null, Array.Empty<object?>());
        Assert.Null(result);
    }

    [Fact]
    public async Task InvokeMethodAsync_AsyncTaskOfTMethod_ReturnsValue()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.AsyncTaskOfString))!;
        var result = await FakeDurableOrchestrationRunner.InvokeMethodAsync(method, null, Array.Empty<object?>());
        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task InvokeMethodAsync_ValueTaskMethod_ReturnsNull()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.ValueTaskVoid))!;
        var result = await FakeDurableOrchestrationRunner.InvokeMethodAsync(method, null, Array.Empty<object?>());
        Assert.Null(result);
    }

    [Fact]
    public async Task InvokeMethodAsync_ValueTaskOfTMethod_ReturnsValue()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.ValueTaskOfInt))!;
        var result = await FakeDurableOrchestrationRunner.InvokeMethodAsync(method, null, Array.Empty<object?>());
        Assert.Equal(99, result);
    }

    [Fact]
    public async Task InvokeMethodAsync_InstanceMethod_InvokesOnTarget()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.GetInstanceValue))!;
        var target = new SampleFunctions("test-value");
        var result = await FakeDurableOrchestrationRunner.InvokeMethodAsync(method, target, Array.Empty<object?>());
        Assert.Equal("test-value", result);
    }

    // -------------------------------------------------------------------------
    // BuildArguments
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildArguments_ActivityTrigger_SetsInputArgument()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.ActivityWithInput))!;
        var functionContext = new MinimalFunctionContext("testFunc");
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var args = FakeDurableOrchestrationRunner.BuildArguments(
            method,
            functionContext,
            CancellationToken.None,
            input: "my-input",
            orchestrationContext: null,
            triggerAttributeType: typeof(ActivityTriggerAttribute),
            serviceProvider: serviceProvider);

        Assert.Single(args);
        Assert.Equal("my-input", args[0]);
    }

    [Fact]
    public void BuildArguments_OrchestrationTrigger_SetsOrchestrationContext()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.OrchestratorWithContext))!;
        var functionContext = new MinimalFunctionContext("testFunc");
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var catalog = new FakeDurableFunctionCatalog(typeof(SampleFunctions).Assembly);
        var entityRunner = new FakeDurableEntityRunner(
            catalog,
            serviceProvider,
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FakeDurableEntityRunner>>());
        var orchestrationContext = new FakeTaskOrchestrationContext(
            "MyOrchestrator", "inst1", null,
            serviceProvider,
            (_, _, _) => Task.FromResult<object?>(null),
            (_, _, _) => Task.FromResult<object?>(null),
            (_, _, _) => Task.FromResult<object?>(null),
            customStatusSink: null,
            entityRunner: entityRunner,
            executionCancellationToken: default);

        var args = FakeDurableOrchestrationRunner.BuildArguments(
            method,
            functionContext,
            CancellationToken.None,
            input: null,
            orchestrationContext: orchestrationContext,
            triggerAttributeType: typeof(OrchestrationTriggerAttribute),
            serviceProvider: serviceProvider);

        entityRunner.Dispose();
        Assert.Single(args);
        Assert.Same(orchestrationContext, args[0]);
    }

    [Fact]
    public void BuildArguments_FunctionContextParam_InjectsFunctionContext()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.MethodWithFunctionContext))!;
        var functionContext = new MinimalFunctionContext("testFunc");
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var args = FakeDurableOrchestrationRunner.BuildArguments(
            method,
            functionContext,
            CancellationToken.None,
            input: null,
            orchestrationContext: null,
            triggerAttributeType: typeof(ActivityTriggerAttribute),
            serviceProvider: serviceProvider);

        Assert.Equal(2, args.Length);
        Assert.Same(functionContext, args[1]);
    }

    [Fact]
    public void BuildArguments_CancellationTokenParam_InjectsCancellationToken()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.MethodWithCancellationToken))!;
        var functionContext = new MinimalFunctionContext("testFunc");
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        using var cts = new CancellationTokenSource();

        var args = FakeDurableOrchestrationRunner.BuildArguments(
            method,
            functionContext,
            cts.Token,
            input: null,
            orchestrationContext: null,
            triggerAttributeType: typeof(ActivityTriggerAttribute),
            serviceProvider: serviceProvider);

        Assert.Equal(2, args.Length);
        Assert.Equal(cts.Token, args[1]);
    }

    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private sealed class SampleFunctions
    {
        public static void VoidMethod() { }
        public static int ReturnInt() => 42;
        public static Task AsyncTask() => Task.CompletedTask;
        public static Task<string> AsyncTaskOfString() => Task.FromResult("hello");
        public static ValueTask ValueTaskVoid() => default;
        public static ValueTask<int> ValueTaskOfInt() => new(99);

        private readonly string _value;
        public SampleFunctions() : this(string.Empty) { }
        public SampleFunctions(string value) => _value = value;
        public string GetInstanceValue() => _value;

        public static string ActivityWithInput([ActivityTrigger] string input) => input;

        public static Task OrchestratorWithContext([OrchestrationTrigger] TaskOrchestrationContext ctx) =>
            Task.CompletedTask;

        public static string MethodWithFunctionContext(
            [ActivityTrigger] string input,
            FunctionContext context) => input;

        public static string MethodWithCancellationToken(
            [ActivityTrigger] string input,
            CancellationToken cancellationToken) => input;
    }

    private sealed class MinimalFunctionContext : FunctionContext
    {
        public MinimalFunctionContext(string functionName)
        {
            FunctionDefinition = new FakeFunctionDef(functionName);
        }

        public override BindingContext BindingContext { get; } = null!;
        public override IInvocationFeatures Features { get; } = null!;
        public override FunctionDefinition FunctionDefinition { get; }
        public override string FunctionId { get; } = Guid.NewGuid().ToString();
        public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();
        public override string InvocationId { get; } = Guid.NewGuid().ToString();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override WorkerRetryContext RetryContext { get; } = null!;
        public override TraceContext TraceContext { get; } = null!;

        private sealed class FakeFunctionDef : FunctionDefinition
        {
            public FakeFunctionDef(string name) { Name = name; }
            public override string EntryPoint { get; } = string.Empty;
            public override string Id { get; } = Guid.NewGuid().ToString();
            public override System.Collections.Immutable.IImmutableDictionary<string, BindingMetadata> InputBindings { get; }
                = System.Collections.Immutable.ImmutableDictionary<string, BindingMetadata>.Empty;
            public override string Name { get; }
            public override System.Collections.Immutable.IImmutableDictionary<string, BindingMetadata> OutputBindings { get; }
                = System.Collections.Immutable.ImmutableDictionary<string, BindingMetadata>.Empty;
            public override System.Collections.Immutable.ImmutableArray<FunctionParameter> Parameters { get; } = [];
            public override string PathToAssembly { get; } = string.Empty;
        }
    }
}
