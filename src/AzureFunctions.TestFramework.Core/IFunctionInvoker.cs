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
    IReadOnlyDictionary<string, FunctionMetadata> GetFunctions();
}
