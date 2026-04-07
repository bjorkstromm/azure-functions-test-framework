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
    Task<FunctionInvocationResult> InvokeAsync(
        string functionName,
        FunctionInvocationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for all discovered functions.
    /// </summary>
    IReadOnlyDictionary<string, IFunctionMetadata> GetFunctions();

    /// <summary>
    /// Registers a trigger binding handler for the specified trigger type.
    /// Calling this method more than once with the same <see cref="ITriggerBinding.TriggerType"/>
    /// is a no-op; the first registration wins.
    /// <para>
    /// Extension packages (e.g. <c>AzureFunctions.TestFramework.Timer</c>) call this at the
    /// start of their <c>InvokeXxxAsync</c> extension methods so that Core knows how to
    /// translate the <see cref="FunctionInvocationContext"/> into a gRPC
    /// <c>InvocationRequest</c> without having hardcoded knowledge of each trigger type.
    /// </para>
    /// </summary>
    void RegisterTriggerBinding(ITriggerBinding binding);
}
