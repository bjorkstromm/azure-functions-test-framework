using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Represents this type.
/// </summary>
public class FakeFunctionContextTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Constructor_InitializesExpectedState()
    {
        var services = new ServiceCollection().BuildServiceProvider();

        var sut = new FakeFunctionContext("MyFunction", services, invocationId: "inv1", traceParent: "tp", traceState: "ts");

        Assert.Equal("inv1", sut.InvocationId);
        Assert.Equal("MyFunction", sut.FunctionDefinition.Name);
        Assert.Equal("tp", sut.TraceContext.TraceParent);
        Assert.Equal("ts", sut.TraceContext.TraceState);
        Assert.Equal(0, sut.RetryContext.MaxRetryCount);
        Assert.Equal(0, sut.RetryContext.RetryCount);
        Assert.Empty(sut.BindingContext.BindingData);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void Features_GetAndSet_Work()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new FakeFunctionContext("MyFunction", services);

        sut.Features.Set("value");
        var resolved = sut.Features.Get<string>();

        Assert.Equal("value", resolved);
    }
}
