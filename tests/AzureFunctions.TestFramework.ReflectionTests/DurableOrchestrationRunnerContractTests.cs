using System.Reflection;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// Contract tests for the dynamic method invocation patterns used in
/// <c>FakeDurableOrchestrationRunner</c>.
///
/// <para>These tests verify that the .NET reflection APIs relied upon to invoke durable
/// function methods (including <c>Task&lt;T&gt;.Result</c> unwrapping and
/// <c>ValueTask&lt;T&gt;.AsTask()</c> conversion) behave as expected.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 8 for full context.</para>
/// </summary>
public class DurableOrchestrationRunnerContractTests
{
    // ----- Task<T>.Result unwrapping -----

    [Fact]
    public void TaskOfT_HasResultProperty()
    {
        var taskType = typeof(Task<int>);
        var resultProp = taskType.GetProperty("Result");
        Assert.NotNull(resultProp);
    }

    [Fact]
    public void TaskOfT_Result_CanBeReadViaReflection()
    {
        var task = Task.FromResult(42);
        var resultProp = task.GetType().GetProperty("Result");
        Assert.NotNull(resultProp);

        var value = resultProp.GetValue(task);
        Assert.Equal(42, value);
    }

    [Fact]
    public void GenericTask_IsGenericType()
    {
        // FakeDurableOrchestrationRunner checks method.ReturnType.IsGenericType before
        // reading .Result. Verify the flag behaves as expected.
        Assert.True(typeof(Task<string>).IsGenericType);
        Assert.False(typeof(Task).IsGenericType);
    }

    // ----- ValueTask<T>.AsTask() -----

    [Fact]
    public void ValueTaskOfT_HasAsTaskMethod()
    {
        var asTaskMethod = typeof(ValueTask<int>).GetMethod(nameof(ValueTask<int>.AsTask));
        Assert.NotNull(asTaskMethod);
        Assert.Empty(asTaskMethod.GetParameters());
    }

    [Fact]
    public void ValueTaskOfT_AsTask_CanBeInvokedViaReflection()
    {
        var valueTask = new ValueTask<int>(99);
        var asTaskMethod = valueTask.GetType().GetMethod(nameof(ValueTask<int>.AsTask))!;

        var task = (Task)asTaskMethod.Invoke(valueTask, Array.Empty<object>())!;
        Assert.NotNull(task);

        // Task should already be complete.
        Assert.True(task.IsCompleted);
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.Equal(99, result);
    }

    [Fact]
    public void ValueTaskOfT_GetGenericTypeDefinition_MatchesExpectedType()
    {
        Assert.Equal(
            typeof(ValueTask<>),
            typeof(ValueTask<string>).GetGenericTypeDefinition());
    }

    // ----- MethodInfo.Invoke basics -----

    [Fact]
    public void MethodInfo_Invoke_CanCallSynchronousMethod()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.SyncActivity),
            BindingFlags.Public | BindingFlags.Static)!;

        var result = method.Invoke(null, new object[] { "hello" });
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public async Task MethodInfo_Invoke_CanCallAsyncMethod()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.AsyncActivity),
            BindingFlags.Public | BindingFlags.Static)!;

        var resultObj = method.Invoke(null, new object[] { "world" });
        Assert.NotNull(resultObj);

        var task = (Task<string>)resultObj!;
        var value = await task;
        Assert.Equal("WORLD", value);
    }

    [Fact]
    public async Task MethodInfo_Invoke_CanCallValueTaskMethod()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.ValueTaskActivity),
            BindingFlags.Public | BindingFlags.Static)!;

        var resultObj = method.Invoke(null, new object[] { "vt" });
        Assert.NotNull(resultObj);

        // Replicate FakeDurableOrchestrationRunner.InvokeMethodAsync ValueTask<T> branch:
        var asTaskMethod = resultObj!.GetType().GetMethod(nameof(ValueTask<string>.AsTask))!;
        var task = (Task<string>)asTaskMethod.Invoke(resultObj, Array.Empty<object>())!;
        var value = await task;
        Assert.Equal("VT", value);
    }

    // ----- Parameter inspection -----

    [Fact]
    public void MethodInfo_GetParameters_WorksForMultipleTypes()
    {
        var method = typeof(SampleFunctions).GetMethod(nameof(SampleFunctions.MultiParamActivity),
            BindingFlags.Public | BindingFlags.Static)!;

        var parameters = method.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal(typeof(int), parameters[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
    }

    /// <summary>Fake function methods that mimic the patterns FakeDurableOrchestrationRunner invokes.</summary>
    private static class SampleFunctions
    {
        public static string SyncActivity(string input) => input.ToUpperInvariant();
        public static Task<string> AsyncActivity(string input) => Task.FromResult(input.ToUpperInvariant());
        public static ValueTask<string> ValueTaskActivity(string input) => new(input.ToUpperInvariant());
        public static string MultiParamActivity(string a, int b, CancellationToken ct) => $"{a}-{b}";
    }
}
