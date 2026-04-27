using System.Reflection;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Durable;

internal sealed class FakeDurableOrchestrationRunner
{
    private readonly FakeDurableFunctionCatalog _catalog;
    private readonly FakeDurableEntityRunner? _entityRunner;
    private readonly FakeDurableExternalEventHub _externalEventHub;
    private readonly ILogger<FakeDurableOrchestrationRunner> _logger;
    private readonly IServiceProvider _serviceProvider;
    private int _subOrchestrationSequence;

    public FakeDurableOrchestrationRunner(
        FakeDurableFunctionCatalog catalog,
        FakeDurableExternalEventHub externalEventHub,
        IServiceProvider serviceProvider,
        ILogger<FakeDurableOrchestrationRunner> logger,
        FakeDurableEntityRunner? entityRunner = null)
    {
        _catalog = catalog;
        _externalEventHub = externalEventHub;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _entityRunner = entityRunner;
    }

    public async Task<object?> RunOrchestrationAsync(
        string orchestratorName,
        string instanceId,
        object? input,
        CancellationToken cancellationToken)
    {
        var result = await RunOrchestrationCoreAsync(
                orchestratorName,
                instanceId,
                input,
                customStatusSink: null,
                cancellationToken)
            .ConfigureAwait(false);
        return result.Output;
    }

    internal async Task<FakeDurableOrchestrationResult> RunOrchestrationWithDetailsAsync(
        string orchestratorName,
        string instanceId,
        object? input,
        Action<object?>? customStatusSink,
        CancellationToken cancellationToken)
    {
        var currentInput = input;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await RunOrchestrationCoreAsync(orchestratorName, instanceId, currentInput, customStatusSink, cancellationToken).ConfigureAwait(false);
            if (!result.IsContinueAsNew)
            {
                return result;
            }

            // Eternal orchestrator pattern: loop with the new input supplied by ContinueAsNew.
            currentInput = result.ContinueAsNewInput;
        }
    }

    internal Task<object?> InvokeActivityAsync(
        string activityName,
        object? input,
        CancellationToken cancellationToken = default)
    {
        return InvokeActivityAsync(new TaskName(activityName), input, cancellationToken);
    }

    private async Task<object?> InvokeActivityAsync(
        TaskName activityName,
        object? input,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Invoking fake durable activity {ActivityName}", activityName.Name);
        var activity = _catalog.GetActivity(activityName.Name);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var functionContext = new FakeFunctionContext(activity.FunctionName, scope.ServiceProvider);
        var target = CreateTarget(activity.Method, scope.ServiceProvider);
        var arguments = BuildArguments(
            activity.Method,
            functionContext,
            cancellationToken,
            input,
            orchestrationContext: null,
            triggerAttributeType: typeof(ActivityTriggerAttribute),
            serviceProvider: scope.ServiceProvider);

        var result = await InvokeMethodAsync(activity.Method, target, arguments);
        _logger.LogInformation("Completed fake durable activity {ActivityName}", activityName.Name);
        return result;
    }

    private async Task<object?> InvokeSubOrchestrationAsync(
        TaskName orchestratorName,
        string parentInstanceId,
        object? input,
        CancellationToken cancellationToken)
    {
        var childInstanceId = CreateSubOrchestrationInstanceId(parentInstanceId, orchestratorName.Name);
        _logger.LogInformation(
            "Invoking fake durable sub-orchestrator {OrchestratorName} for parent {ParentInstanceId} as child {ChildInstanceId}",
            orchestratorName.Name,
            parentInstanceId,
            childInstanceId);

        var result = await RunOrchestrationCoreAsync(orchestratorName.Name, childInstanceId, input, customStatusSink: null, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Completed fake durable sub-orchestrator {OrchestratorName} for parent {ParentInstanceId} as child {ChildInstanceId}",
            orchestratorName.Name,
            parentInstanceId,
            childInstanceId);

        return result.Output;
    }

    private async Task<FakeDurableOrchestrationResult> RunOrchestrationCoreAsync(
        string orchestratorName,
        string instanceId,
        object? input,
        Action<object?>? customStatusSink,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running fake durable orchestrator {OrchestratorName} for instance {InstanceId}", orchestratorName, instanceId);
        var orchestrator = _catalog.GetOrchestrator(orchestratorName);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var functionContext = new FakeFunctionContext(orchestrator.FunctionName, scope.ServiceProvider);
        var orchestrationContext = new FakeTaskOrchestrationContext(
            orchestratorName,
            instanceId,
            input,
            scope.ServiceProvider,
            InvokeActivityAsync,
            (childName, childInput, childCancellationToken) =>
                InvokeSubOrchestrationAsync(childName, instanceId, childInput, childCancellationToken),
            (waitingInstanceId, eventName, eventCancellationToken) =>
                _externalEventHub.WaitForEventAsync(waitingInstanceId, eventName, eventCancellationToken),
            customStatusSink,
            _entityRunner,
            executionCancellationToken: cancellationToken);

        var target = CreateTarget(orchestrator.Method, scope.ServiceProvider);
        var arguments = BuildArguments(
            orchestrator.Method,
            functionContext,
            cancellationToken,
            input,
            orchestrationContext,
            triggerAttributeType: typeof(OrchestrationTriggerAttribute),
            serviceProvider: scope.ServiceProvider);

        var result = await InvokeMethodAsync(orchestrator.Method, target, arguments);
        _logger.LogInformation("Completed fake durable orchestrator {OrchestratorName} for instance {InstanceId}", orchestratorName, instanceId);

        return orchestrationContext.IsContinueAsNew
            ? new FakeDurableOrchestrationResult(null, orchestrationContext.CustomStatus, IsContinueAsNew: true, ContinueAsNewInput: orchestrationContext.ContinueAsNewInput)
            : new FakeDurableOrchestrationResult(result, orchestrationContext.CustomStatus);
    }

    private string CreateSubOrchestrationInstanceId(string parentInstanceId, string orchestratorName)
    {
        var sequence = Interlocked.Increment(ref _subOrchestrationSequence);
        return $"{parentInstanceId}:{orchestratorName}:{sequence}";
    }

    private static object? CreateTarget(MethodInfo method, IServiceProvider serviceProvider)
    {
        if (method.IsStatic)
        {
            return null;
        }

        return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, method.DeclaringType!);
    }

    private static object?[] BuildArguments(
        MethodInfo method,
        FunctionContext functionContext,
        CancellationToken cancellationToken,
        object? input,
        TaskOrchestrationContext? orchestrationContext,
        Type triggerAttributeType,
        IServiceProvider serviceProvider)
    {
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.GetCustomAttributes().Any(attribute => triggerAttributeType.IsInstanceOfType(attribute)))
            {
                arguments[i] = triggerAttributeType == typeof(OrchestrationTriggerAttribute)
                    ? orchestrationContext
                    : ConvertValue(input, parameter.ParameterType);
                continue;
            }

            if (parameter.ParameterType == typeof(FunctionContext))
            {
                arguments[i] = functionContext;
                continue;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                arguments[i] = cancellationToken;
                continue;
            }

            // [DurableClient] parameters resolve from DI — DurableTaskClient is an abstract class
            // and cannot be deserialized from JSON.
            if (parameter.GetCustomAttribute<DurableClientAttribute>() is not null
                && typeof(DurableTaskClient).IsAssignableFrom(parameter.ParameterType))
            {
                arguments[i] = serviceProvider.GetRequiredService<DurableTaskClient>();
                continue;
            }

            arguments[i] = ConvertValue(input, parameter.ParameterType);
        }

        return arguments;
    }

    private static async Task<object?> InvokeMethodAsync(MethodInfo method, object? target, object?[] arguments)
    {
        var result = method.Invoke(target, arguments);
        if (result is null)
        {
            return null;
        }

        if (result is Task task)
        {
            await task.ConfigureAwait(false);

            if (method.ReturnType.IsGenericType)
            {
                return method.ReturnType.GetProperty("Result")?.GetValue(result);
            }

            return null;
        }

        if (method.ReturnType == typeof(ValueTask))
        {
            await ((ValueTask)result).ConfigureAwait(false);
            return null;
        }

        if (method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTaskMethod = method.ReturnType.GetMethod(nameof(ValueTask<int>.AsTask))!;
            var asTask = (Task)asTaskMethod.Invoke(result, Array.Empty<object>())!;
            await asTask.ConfigureAwait(false);
            return asTask.GetType().GetProperty("Result")?.GetValue(asTask);
        }

        return result;
    }

    internal static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
        {
            if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) is null)
            {
                return Activator.CreateInstance(targetType);
            }

            return null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        var json = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize(json, targetType);
    }

    internal sealed record FakeDurableOrchestrationResult(
        object? Output,
        object? CustomStatus,
        bool IsContinueAsNew = false,
        object? ContinueAsNewInput = null);
}
