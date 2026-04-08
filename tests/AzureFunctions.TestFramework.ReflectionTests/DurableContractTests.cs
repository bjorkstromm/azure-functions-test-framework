using System.Collections;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// SDK contract tests for the Durable extensions reflection in
/// <c>FunctionsTestHostBuilderDurableExtensions</c>.
///
/// <para>These tests verify that the internal Durable extension types and members that the
/// framework depends on via reflection are still present in the Durable extensions assembly.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 6 for full context.</para>
/// </summary>
public class DurableContractTests
{
    private const string DurableTaskClientConverterName =
        "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.DurableTaskClientConverter";

    private const string FunctionsDurableClientProviderName =
        "Microsoft.Azure.Functions.Worker.Extensions.DurableTask.FunctionsDurableClientProvider";

    // DurableClientAttribute is public and lives in the same assembly as the internals.
    private static Assembly DurableExtensionsAssembly =>
        typeof(DurableClientAttribute).Assembly;

    [Fact]
    public void DurableTaskClientConverter_TypeExistsInDurableExtensionsAssembly()
    {
        var type = DurableExtensionsAssembly.GetType(DurableTaskClientConverterName);
        Assert.NotNull(type);
    }

    [Fact]
    public void FunctionsDurableClientProvider_TypeExistsInDurableExtensionsAssembly()
    {
        var type = DurableExtensionsAssembly.GetType(FunctionsDurableClientProviderName);
        Assert.NotNull(type);
    }

    [Fact]
    public void FunctionsDurableClientProvider_HasClientsPrivateField()
    {
        var providerType = DurableExtensionsAssembly.GetType(FunctionsDurableClientProviderName);
        Assert.NotNull(providerType);

        var clientsField = providerType.GetField(
            "clients",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(clientsField);
        Assert.True(typeof(IDictionary).IsAssignableFrom(clientsField.FieldType),
            "The 'clients' field should be a dictionary type.");
    }

    [Fact]
    public void FunctionsDurableClientProvider_HasClientKeyNestedType()
    {
        var providerType = DurableExtensionsAssembly.GetType(FunctionsDurableClientProviderName);
        Assert.NotNull(providerType);

        var clientKeyType = providerType.GetNestedType("ClientKey", BindingFlags.NonPublic);
        Assert.NotNull(clientKeyType);
    }

    [Fact]
    public void FunctionsDurableClientProvider_ClientKey_HasThreeParameterConstructor()
    {
        var providerType = DurableExtensionsAssembly.GetType(FunctionsDurableClientProviderName);
        Assert.NotNull(providerType);

        var clientKeyType = providerType.GetNestedType("ClientKey", BindingFlags.NonPublic);
        Assert.NotNull(clientKeyType);

        // Expected: ClientKey(Uri endpoint, string taskHub, string connectionName)
        var ctor = clientKeyType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 3
                    && ps[0].ParameterType == typeof(Uri)
                    && ps[1].ParameterType == typeof(string)
                    && ps[2].ParameterType == typeof(string);
            });

        Assert.NotNull(ctor);
    }

    [Fact]
    public void FunctionsDurableClientProvider_HasClientHolderNestedType()
    {
        var providerType = DurableExtensionsAssembly.GetType(FunctionsDurableClientProviderName);
        Assert.NotNull(providerType);

        var clientHolderType = providerType.GetNestedType("ClientHolder", BindingFlags.NonPublic);
        Assert.NotNull(clientHolderType);
    }

    [Fact]
    public void FunctionsDurableClientProvider_ClientHolder_HasTwoParameterConstructorWithDurableTaskClient()
    {
        var providerType = DurableExtensionsAssembly.GetType(FunctionsDurableClientProviderName);
        Assert.NotNull(providerType);

        var clientHolderType = providerType.GetNestedType("ClientHolder", BindingFlags.NonPublic);
        Assert.NotNull(clientHolderType);

        // Expected: ClientHolder(DurableTaskClient client, <secondArg>)
        var ctor = clientHolderType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 2
                    && ps[0].ParameterType == typeof(DurableTaskClient);
            });

        Assert.NotNull(ctor);
    }

    [Fact]
    public void FunctionsDurableClientProvider_CanBeConstructedViaReflection()
    {
        // This replicates what FunctionsTestHostBuilderDurableExtensions does to verify
        // the constructor signature (ILoggerFactory, IOptions<DurableTaskClientOptions>) is stable.
        var providerType = DurableExtensionsAssembly.GetType(FunctionsDurableClientProviderName);
        Assert.NotNull(providerType);

        // We just verify a constructor with 2 parameters exists (exact types can vary by version).
        var ctors = providerType.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotEmpty(ctors);

        var twoParamCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == 2);
        Assert.NotNull(twoParamCtor);
    }
}
