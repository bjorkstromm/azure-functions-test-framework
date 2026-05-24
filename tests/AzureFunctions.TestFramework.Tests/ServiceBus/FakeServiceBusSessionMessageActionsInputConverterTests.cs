using AzureFunctions.TestFramework.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.ServiceBus;

/// <summary>
/// Represents this type.
/// </summary>
public class FakeServiceBusSessionMessageActionsInputConverterTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task ConvertAsync_TargetTypeMatches_ReturnsRegisteredFakeActions()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<FakeServiceBusSessionMessageActions>()
            .BuildServiceProvider();
        var sut = new FakeServiceBusSessionMessageActionsInputConverter(services);
        var context = new FakeConverterContext(typeof(Microsoft.Azure.Functions.Worker.ServiceBusSessionMessageActions));

        var result = await sut.ConvertAsync(context);

        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        var resolved = Assert.IsType<FakeServiceBusSessionMessageActions>(result.Value);
        Assert.Same(services.GetRequiredService<FakeServiceBusSessionMessageActions>(), resolved);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task ConvertAsync_TargetTypeDoesNotMatch_ReturnsUnhandled()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<FakeServiceBusSessionMessageActions>()
            .BuildServiceProvider();
        var sut = new FakeServiceBusSessionMessageActionsInputConverter(services);
        var context = new FakeConverterContext(typeof(string));

        var result = await sut.ConvertAsync(context);

        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_MissingDependencies_Throws()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => new FakeServiceBusSessionMessageActionsInputConverter(services));
    }

    private sealed class FakeConverterContext : ConverterContext
    {
        public FakeConverterContext(Type targetType) => TargetType = targetType;

        public override Type TargetType { get; }
        public override object? Source => null;
        public override FunctionContext FunctionContext { get; } = new MinimalFunctionContext();
        public override IReadOnlyDictionary<string, object> Properties { get; } = new Dictionary<string, object>();
    }

    private sealed class MinimalFunctionContext : FunctionContext
    {
        public override string InvocationId { get; } = Guid.NewGuid().ToString("N");
        public override string FunctionId { get; } = Guid.NewGuid().ToString("N");
        public override TraceContext TraceContext { get; } = null!;
        public override BindingContext BindingContext { get; } = null!;
        public override RetryContext RetryContext { get; } = null!;
        public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().AddLogging().BuildServiceProvider();
        public override FunctionDefinition FunctionDefinition { get; } = null!;
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IInvocationFeatures Features { get; } = null!;
    }
}
