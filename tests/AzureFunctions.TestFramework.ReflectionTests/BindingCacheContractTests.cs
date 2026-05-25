using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Azure.Functions.Worker.Converters;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// SDK contract tests for <c>BindingCacheCleaner</c>.
///
/// <para>These tests verify that the internal <c>IBindingCache&lt;T&gt;</c> interface and
/// the private <c>ConcurrentDictionary</c> field inside its implementation are still present
/// in the Worker SDK assembly.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 3 for full context.</para>
/// </summary>
public class BindingCacheContractTests
{
    // ConversionResult is public and lives in the Worker SDK core assembly.
    private static Assembly WorkerAssembly => typeof(ConversionResult).Assembly;

    [Fact]
    public void IBindingCache_GenericInterfaceExistsInWorkerAssembly()
    {
        var cacheOpenType = WorkerAssembly.GetTypes()
            .FirstOrDefault(t =>
                t.IsInterface
                && t.IsGenericTypeDefinition
                && t.Name == "IBindingCache`1");

        Assert.NotNull(cacheOpenType);
    }

    [Fact]
    public void IBindingCache_CanBeClosedWithConversionResult()
    {
        var cacheOpenType = WorkerAssembly.GetTypes()
            .First(t =>
                t.IsInterface
                && t.IsGenericTypeDefinition
                && t.Name == "IBindingCache`1");

        var closedType = cacheOpenType.MakeGenericType(typeof(ConversionResult));
        Assert.NotNull(closedType);
    }

    [Fact]
    public void IBindingCache_HasConcreteImplementationInWorkerAssembly()
    {
        var cacheOpenType = WorkerAssembly.GetTypes()
            .First(t =>
                t.IsInterface
                && t.IsGenericTypeDefinition
                && t.Name == "IBindingCache`1");

        // DefaultBindingCache<T> is itself an open generic type; we search by open-generic
        // interface matching (the same way BindingCacheCleaner works at runtime).
        var implementations = WorkerAssembly.GetTypes()
            .Where(t =>
                !t.IsInterface && !t.IsAbstract
                && t.GetInterfaces().Any(iface =>
                    iface.IsGenericType
                    && iface.GetGenericTypeDefinition() == cacheOpenType))
            .ToList();

        Assert.NotEmpty(implementations);
    }

    [Fact]
    public void IBindingCache_Implementation_HasPrivateConcurrentDictionaryField()
    {
        var cacheOpenType = WorkerAssembly.GetTypes()
            .First(t =>
                t.IsInterface
                && t.IsGenericTypeDefinition
                && t.Name == "IBindingCache`1");

        var implementations = WorkerAssembly.GetTypes()
            .Where(t =>
                !t.IsInterface && !t.IsAbstract
                && t.GetInterfaces().Any(iface =>
                    iface.IsGenericType
                    && iface.GetGenericTypeDefinition() == cacheOpenType))
            .ToList();

        // At least one implementation must expose a private ConcurrentDictionary<,> field.
        var hasDictField = implementations.Any(impl =>
            impl.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(f =>
                    f.FieldType.IsGenericType &&
                    f.FieldType.GetGenericTypeDefinition() == typeof(ConcurrentDictionary<,>)));

        Assert.True(hasDictField,
            "Expected at least one IBindingCache implementation to contain a " +
            "private ConcurrentDictionary field. BindingCacheCleaner's reflection will break.");
    }

    [Fact]
    public void ConcurrentDictionary_HasClearMethod()
    {
        // This is a .NET BCL guarantee, but included for completeness — BindingCacheCleaner
        // calls dict.GetType().GetMethod("Clear") and asserts it exists.
        var clearMethod = typeof(ConcurrentDictionary<string, string>)
            .GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(clearMethod);
    }
}
