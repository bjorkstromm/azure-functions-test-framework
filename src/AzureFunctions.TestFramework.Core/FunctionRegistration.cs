namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Represents the metadata of a discovered Azure Function, used to look up
/// the gRPC function ID and trigger binding parameter name when building
/// invocation requests.
/// </summary>
/// <param name="FunctionId">The stable hash-based function ID assigned by the worker's source-generated metadata provider.</param>
/// <param name="FunctionName">The function name as declared on the function method (case-insensitive).</param>
/// <param name="TriggerType">The trigger type string from the binding metadata (e.g. <c>"timerTrigger"</c>, <c>"queueTrigger"</c>).</param>
/// <param name="ParameterName">The parameter name for the trigger binding as declared in the function signature (e.g. <c>"myTimer"</c>, <c>"myQueueItem"</c>).</param>
public sealed record FunctionRegistration(
    string FunctionId,
    string FunctionName,
    string TriggerType,
    string ParameterName);
