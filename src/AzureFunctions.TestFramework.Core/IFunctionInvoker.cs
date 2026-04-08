using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;

namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Handles invocation of Azure Functions with proper binding data.
/// </summary>
public interface IFunctionInvoker
{
    /// <summary>
    /// Invokes a function by name with the specified invocation context.
    /// </summary>
    /// <param name="functionName">The name of the function to invoke (case-insensitive).</param>
    /// <param name="context">The invocation context containing trigger input data.</param>
    /// <param name="triggerBindingFactory">
    /// A factory that converts the <see cref="FunctionInvocationContext"/> and resolved
    /// <see cref="FunctionRegistration"/> into the <see cref="TriggerBindingData"/> that is
    /// sent to the worker as the gRPC <c>InvocationRequest</c>.
    /// Extension packages (e.g. <c>AzureFunctions.TestFramework.Timer</c>) supply a private
    /// static method from their extension class as this argument.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FunctionInvocationResult> InvokeAsync(
        string functionName,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, FunctionRegistration, TriggerBindingData> triggerBindingFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for all discovered functions.
    /// </summary>
    IReadOnlyDictionary<string, IFunctionMetadata> GetFunctions();
}
