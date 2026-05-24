using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Core;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// SDK contract tests for the extension startup invocation performed by
/// <c>WorkerHostService.InvokeExtensionStartupCode</c>.
///
/// <para>The framework reads <see cref="WorkerExtensionStartupCodeExecutorInfoAttribute"/>
/// from the functions assembly and invokes the executor's
/// <see cref="WorkerExtensionStartup.Configure"/> method.  These tests verify
/// that the public SDK types and members the framework depends on are still present.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 10 for full context.</para>
/// </summary>
public class ExtensionStartupContractTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WorkerExtensionStartupCodeExecutorInfoAttribute_IsPublicClass()
    {
        var type = typeof(WorkerExtensionStartupCodeExecutorInfoAttribute);

        Assert.True(type.IsPublic);
        Assert.True(type.IsClass);
        Assert.True(typeof(Attribute).IsAssignableFrom(type));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WorkerExtensionStartupCodeExecutorInfoAttribute_HasStartupCodeExecutorTypeProperty()
    {
        var prop = typeof(WorkerExtensionStartupCodeExecutorInfoAttribute)
            .GetProperty("StartupCodeExecutorType", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(prop);
        Assert.Equal(typeof(Type), prop.PropertyType);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WorkerExtensionStartup_IsPublicAbstractClass()
    {
        var type = typeof(WorkerExtensionStartup);

        Assert.True(type.IsPublic);
        Assert.True(type.IsClass);
        Assert.True(type.IsAbstract);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void WorkerExtensionStartup_HasConfigureMethod()
    {
        var method = typeof(WorkerExtensionStartup).GetMethod(
            "Configure",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);

        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(IFunctionsWorkerApplicationBuilder), parameters[0].ParameterType);
    }
}
