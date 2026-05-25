using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// Contract tests for the <c>IAutoConfigureStartup</c> scan performed by
/// <c>WorkerHostService</c> and for the general-purpose durable function attribute
/// discovery in <c>FakeDurableFunctionCatalog</c>.
///
/// <para>See <c>docs/Reflection.md</c> §§ 5, 7 for context.</para>
/// </summary>
public class AutoStartupAndAttributeContractTests
{
    [Fact]
    public void IAutoConfigureStartup_IsPublicInterface()
    {
        // WorkerHostService scans for types implementing IAutoConfigureStartup.
        // If this interface becomes internal or moves namespace, the scan breaks.
        var type = typeof(IAutoConfigureStartup);

        Assert.True(type.IsInterface);
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void IAutoConfigureStartup_HasConfigureMethod()
    {
        var configureMethod = typeof(IAutoConfigureStartup).GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(configureMethod);
    }

    [Fact]
    public void FunctionAttribute_IsPublicAndUsable()
    {
        // FakeDurableFunctionCatalog uses [Function] attribute to discover durable functions.
        var attr = typeof(FunctionAttribute);
        Assert.True(attr.IsPublic);
        Assert.True(Attribute.IsDefined(attr, typeof(AttributeUsageAttribute)));
    }

    [Fact]
    public void ActivityTriggerAttribute_IsPublicAndUsable()
    {
        var attr = typeof(ActivityTriggerAttribute);
        Assert.True(attr.IsPublic);
    }

    [Fact]
    public void OrchestrationTriggerAttribute_IsPublicAndUsable()
    {
        var attr = typeof(OrchestrationTriggerAttribute);
        Assert.True(attr.IsPublic);
    }

    [Fact]
    public void EntityTriggerAttribute_IsPublicAndUsable()
    {
        var attr = typeof(EntityTriggerAttribute);
        Assert.True(attr.IsPublic);
    }

    [Fact]
    public void DurableClientAttribute_IsPublicAndUsable()
    {
        var attr = typeof(DurableClientAttribute);
        Assert.True(attr.IsPublic);
    }
}
