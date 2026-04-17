using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// SDK contract tests for <c>WorkerHostService.GetPipelineMiddlewareList</c>.
///
/// <para>In the IHostBuilder + factory path, <c>InvokeExtensionStartupCodeAtFront</c>
/// reflects into the pipeline builder's internal middleware collection to insert
/// extension middleware at the front.  These tests verify the internal field chain
/// is still present.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 11 for full context.</para>
/// </summary>
public class PipelineBuilderContractTests
{
    /// <summary>
    /// The Worker.Core assembly that contains <c>FunctionsWorkerApplicationBuilder</c>
    /// and <c>DefaultInvocationPipelineBuilder&lt;T&gt;</c>.
    /// </summary>
    private static Assembly WorkerCoreAssembly => typeof(WorkerOptions).Assembly;

    [Fact]
    public void FunctionsWorkerApplicationBuilder_ExistsInWorkerCoreAssembly()
    {
        // The concrete type is internal — find it by name.
        var type = WorkerCoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "FunctionsWorkerApplicationBuilder" && !t.IsInterface);

        Assert.NotNull(type);
    }

    [Fact]
    public void FunctionsWorkerApplicationBuilder_Has_PipelineBuilderField()
    {
        var fabType = WorkerCoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "FunctionsWorkerApplicationBuilder" && !t.IsInterface);

        Assert.NotNull(fabType);

        var field = fabType.GetField("_pipelineBuilder",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
    }

    [Fact]
    public void DefaultInvocationPipelineBuilder_ExistsInWorkerCoreAssembly()
    {
        var type = WorkerCoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name.StartsWith("DefaultInvocationPipelineBuilder"));

        Assert.NotNull(type);
    }

    [Fact]
    public void DefaultInvocationPipelineBuilder_Has_MiddlewareCollectionField()
    {
        var pipelineType = WorkerCoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name.StartsWith("DefaultInvocationPipelineBuilder"));

        Assert.NotNull(pipelineType);

        // Close the generic if needed (the type is generic: DefaultInvocationPipelineBuilder<T>)
        var closedType = pipelineType.IsGenericTypeDefinition
            ? pipelineType.MakeGenericType(typeof(FunctionContext))
            : pipelineType;

        var field = closedType.GetField("_middlewareCollection",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
    }

    [Fact]
    public void MiddlewareCollectionRuntimeValue_ImplementsIList()
    {
        // The declared field type is IList<Func<FunctionExecutionDelegate, FunctionExecutionDelegate>>
        // which does not inherit from the non-generic IList.  However, the concrete List<T>
        // assigned at runtime DOES implement IList, and the framework casts to IList for
        // Insert / RemoveAt operations.  Verify this by creating an instance.
        var pipelineType = WorkerCoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name.StartsWith("DefaultInvocationPipelineBuilder"));

        Assert.NotNull(pipelineType);

        var closedType = pipelineType.IsGenericTypeDefinition
            ? pipelineType.MakeGenericType(typeof(FunctionContext))
            : pipelineType;

        var instance = Activator.CreateInstance(closedType, nonPublic: true);
        Assert.NotNull(instance);

        var field = closedType.GetField("_middlewareCollection",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var value = field.GetValue(instance);
        Assert.NotNull(value);
        Assert.IsAssignableFrom<System.Collections.IList>(value);
    }
}
