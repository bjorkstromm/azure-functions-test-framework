using AzureFunctions.TestFramework.Core.Worker.Converters;
using AzureFunctions.TestFramework.Durable;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="TestHttpRequestConverter"/>.
/// </summary>
public class TestHttpRequestConverterTests
{
    private readonly TestHttpRequestConverter _converter = new();

    [Fact]
    public async Task ConvertAsync_WrongTargetType_ReturnsUnhandled()
    {
        var context = new FakeConverterContext(typeof(string));
        var result = await _converter.ConvertAsync(context);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_HttpRequestTarget_NoItemsKey_ReturnsUnhandled()
    {
        var context = new FakeConverterContext(typeof(HttpRequest));
        var result = await _converter.ConvertAsync(context);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_HttpRequestTarget_NullItemValue_ReturnsUnhandled()
    {
        var functionContext = new MinimalFunctionContext();
        functionContext.Items["HttpRequestContext"] = null!;
        var context = new FakeConverterContext(typeof(HttpRequest), functionContext);
        var result = await _converter.ConvertAsync(context);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_HttpRequestTarget_ValidHttpContext_ReturnsSuccess()
    {
        var httpContext = new DefaultHttpContext();
        var functionContext = new MinimalFunctionContext();
        functionContext.Items["HttpRequestContext"] = httpContext;

        var context = new FakeConverterContext(typeof(HttpRequest), functionContext);
        var result = await _converter.ConvertAsync(context);

        Assert.Equal(ConversionStatus.Succeeded, result.Status);
        Assert.IsAssignableFrom<HttpRequest>(result.Value);
    }

    [Fact]
    public async Task ConvertAsync_HttpRequestTarget_ObjectWithNoRequestProp_ReturnsUnhandled()
    {
        var functionContext = new MinimalFunctionContext();
        functionContext.Items["HttpRequestContext"] = new object(); // no "Request" property
        var context = new FakeConverterContext(typeof(HttpRequest), functionContext);
        var result = await _converter.ConvertAsync(context);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    [Fact]
    public async Task ConvertAsync_WrongTargetType_IntType_ReturnsUnhandled()
    {
        var functionContext = new MinimalFunctionContext();
        functionContext.Items["HttpRequestContext"] = new DefaultHttpContext();
        var context = new FakeConverterContext(typeof(int), functionContext);
        var result = await _converter.ConvertAsync(context);
        Assert.Equal(ConversionStatus.Unhandled, result.Status);
    }

    /// <summary>
    /// A minimal stub <see cref="FunctionContext"/> for use in unit tests,
    /// implementing only the <see cref="Items"/> dictionary which is all
    /// <see cref="TestHttpRequestConverter"/> reads.
    /// </summary>
    private sealed class MinimalFunctionContext : FunctionContext
    {
        public override BindingContext BindingContext { get; } = null!;
        public override IInvocationFeatures Features { get; } = null!;
        public override FunctionDefinition FunctionDefinition { get; } = null!;
        public override string FunctionId { get; } = Guid.NewGuid().ToString();
        public override IServiceProvider InstanceServices { get; set; } = new ServiceCollection().BuildServiceProvider();
        public override string InvocationId { get; } = Guid.NewGuid().ToString();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override RetryContext RetryContext { get; } = null!;
        public override TraceContext TraceContext { get; } = null!;
    }

    /// <summary>Minimal <see cref="ConverterContext"/> stub for unit tests.</summary>
    private sealed class FakeConverterContext : ConverterContext
    {
        private readonly FunctionContext _functionContext;

        public FakeConverterContext(Type targetType, FunctionContext? functionContext = null)
        {
            TargetType = targetType;
            _functionContext = functionContext ?? new MinimalFunctionContext();
        }

        public override Type TargetType { get; }
        public override object? Source { get; } = null;
        public override FunctionContext FunctionContext => _functionContext;
        public override IReadOnlyDictionary<string, object> Properties { get; } =
            new Dictionary<string, object>();
    }
}
