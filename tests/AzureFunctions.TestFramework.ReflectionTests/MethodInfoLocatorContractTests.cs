using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Converters;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// SDK contract tests for <c>InProcessMethodInfoLocator</c>.
///
/// <para>These tests verify that the internal Worker SDK types and members that
/// <see cref="InProcessMethodInfoLocator"/> depends on via reflection are still present.
/// A failure here means the SDK was updated in a breaking way and the framework's
/// reflection code must be updated accordingly.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 1 for full context.</para>
/// </summary>
public class MethodInfoLocatorContractTests
{
    private const string InterfaceFullName =
        "Microsoft.Azure.Functions.Worker.Invocation.IMethodInfoLocator";

    private static Assembly WorkerCoreAssembly => typeof(WorkerOptions).Assembly;

    [Fact]
    public void IMethodInfoLocator_ExistsInWorkerCoreAssembly()
    {
        var type = WorkerCoreAssembly.GetType(InterfaceFullName);
        Assert.NotNull(type);
        Assert.True(type.IsInterface, $"{InterfaceFullName} should be an interface.");
    }

    [Fact]
    public void IMethodInfoLocator_HasGetMethodWithTwoStringParameters()
    {
        var locatorInterface = WorkerCoreAssembly.GetType(InterfaceFullName);
        Assert.NotNull(locatorInterface);

        var getMethod = locatorInterface.GetMethod(
            "GetMethod",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(getMethod);

        var parameters = getMethod.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(MethodInfo), getMethod.ReturnType);
    }

    [Fact]
    public void DispatchProxy_Create_CanBeInvokedWithInternalInterface()
    {
        // This verifies that DispatchProxy.Create<TInterface, TProxy> can be called via
        // reflection with an internal interface type — the exact pattern used in
        // InProcessMethodInfoLocator.TryRegister.
        var locatorInterface = WorkerCoreAssembly.GetType(InterfaceFullName);
        Assert.NotNull(locatorInterface);

        var createMethod = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "Create" && m.GetGenericArguments().Length == 2);

        Assert.NotNull(createMethod);

        var closedCreate = createMethod.MakeGenericMethod(locatorInterface, typeof(NoOpDispatchProxy));
        var proxy = closedCreate.Invoke(null, null);
        Assert.NotNull(proxy);
        Assert.IsAssignableFrom(locatorInterface, proxy);
    }

    /// <summary>Minimal DispatchProxy subclass used to validate proxy creation.</summary>
    public class NoOpDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException("NoOpDispatchProxy should not be invoked in this test.");
    }
}
