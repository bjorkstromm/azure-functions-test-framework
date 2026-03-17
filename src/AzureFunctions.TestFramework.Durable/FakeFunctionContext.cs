using System.Collections;
using System.Collections.Immutable;
using Microsoft.Azure.Functions.Worker;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeFunctionContext : FunctionContext
{
    public FakeFunctionContext(
        string functionName,
        IServiceProvider serviceProvider,
        string? invocationId = null,
        string? traceParent = null,
        string? traceState = null)
    {
        InvocationId = invocationId ?? Guid.NewGuid().ToString("N");
        FunctionId = Guid.NewGuid().ToString("N");
        TraceContext = new FakeTraceContext(
            traceParent ?? Guid.NewGuid().ToString("N"),
            traceState ?? Guid.NewGuid().ToString("N"));
        BindingContext = new FakeBindingContext();
        RetryContext = new FakeRetryContext();
        InstanceServices = serviceProvider;
        FunctionDefinition = new FakeFunctionDefinition(functionName);
        Features = new FakeInvocationFeatures();
        Items = new Dictionary<object, object>();
    }

    public override BindingContext BindingContext { get; }

    public override IInvocationFeatures Features { get; }

    public override FunctionDefinition FunctionDefinition { get; }

    public override string FunctionId { get; }

    public override IServiceProvider InstanceServices { get; set; }

    public override string InvocationId { get; }

    public override IDictionary<object, object> Items { get; set; }

    public override RetryContext RetryContext { get; }

    public override TraceContext TraceContext { get; }

    private sealed class FakeTraceContext : TraceContext
    {
        public FakeTraceContext(string traceParent, string traceState)
        {
            TraceParent = traceParent;
            TraceState = traceState;
        }

        public override string TraceParent { get; }

        public override string TraceState { get; }
    }

    private sealed class FakeBindingContext : BindingContext
    {
        public override IReadOnlyDictionary<string, object?> BindingData { get; } = new Dictionary<string, object?>();
    }

    private sealed class FakeRetryContext : RetryContext
    {
        public override int MaxRetryCount => 0;

        public override int RetryCount => 0;
    }

    private sealed class FakeFunctionDefinition : FunctionDefinition
    {
        public FakeFunctionDefinition(string name)
        {
            Name = name;
        }

        public override string EntryPoint { get; } = string.Empty;

        public override string Id { get; } = Guid.NewGuid().ToString("N");

        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } =
            ImmutableDictionary<string, BindingMetadata>.Empty;

        public override string Name { get; }

        public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } =
            ImmutableDictionary<string, BindingMetadata>.Empty;

        public override ImmutableArray<FunctionParameter> Parameters { get; } = [];

        public override string PathToAssembly { get; } = string.Empty;
    }

    private sealed class FakeInvocationFeatures : Dictionary<Type, object>, IInvocationFeatures
    {
        public T? Get<T>()
        {
            return TryGetValue(typeof(T), out var value) ? (T?)value : default;
        }

        public void Set<T>(T instance)
        {
            this[typeof(T)] = instance!;
        }

        IEnumerator<KeyValuePair<Type, object>> IEnumerable<KeyValuePair<Type, object>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
