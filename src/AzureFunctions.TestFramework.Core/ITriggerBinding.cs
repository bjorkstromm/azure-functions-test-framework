namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Extension point that converts a <see cref="FunctionInvocationContext"/> into
/// <see cref="TriggerBindingData"/> suitable for a gRPC <c>InvocationRequest</c>.
/// <para>
/// One implementation exists per trigger type (timerTrigger, queueTrigger, etc.) and lives
/// in the corresponding extension package
/// (<c>AzureFunctions.TestFramework.Timer</c>, <c>AzureFunctions.TestFramework.Queue</c>, etc.).
/// Register an implementation via <see cref="IFunctionInvoker.RegisterTriggerBinding"/>.
/// </para>
/// </summary>
public interface ITriggerBinding
{
    /// <summary>
    /// Gets the trigger type string this binding handles
    /// (e.g. <c>"timerTrigger"</c>, <c>"queueTrigger"</c>).
    /// </summary>
    string TriggerType { get; }

    /// <summary>
    /// Builds the <see cref="TriggerBindingData"/> for an invocation request from the supplied
    /// <paramref name="context"/> and resolved <paramref name="function"/> registration.
    /// </summary>
    /// <param name="context">The invocation context populated by the caller (e.g. an extension method).</param>
    /// <param name="function">The resolved function registration containing the function ID and trigger parameter name.</param>
    TriggerBindingData CreateBindingData(FunctionInvocationContext context, FunctionRegistration function);
}
