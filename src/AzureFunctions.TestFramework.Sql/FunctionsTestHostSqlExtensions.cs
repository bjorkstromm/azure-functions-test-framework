using System.Text.Json;
using AzureFunctions.TestFramework.Core;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;

namespace AzureFunctions.TestFramework.Sql;

/// <summary>
/// Extension methods for invoking SQL-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostSqlExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a SQL-triggered function by name with the specified list of row changes.
    /// </summary>
    /// <typeparam name="T">
    /// The row type. Each <see cref="SqlChange{T}"/> is serialized to JSON; the worker
    /// deserializes the resulting JSON array into the function parameter
    /// (e.g. <c>IReadOnlyList&lt;SqlChange&lt;T&gt;&gt;</c>).
    /// </typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the SQL trigger function (case-insensitive).</param>
    /// <param name="changes">
    /// The batch of row changes to simulate as the SQL trigger input.
    /// Must contain at least one change.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeSqlAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<SqlChange<T>> changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
            throw new ArgumentException("The changes list must contain at least one change.", nameof(changes));

        var jsonArray = JsonSerializer.Serialize(changes, _jsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "sqlTrigger",
            InputData = { ["$changesJson"] = jsonArray }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes a SQL-triggered function by name with the specified raw JSON array of row changes.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the SQL trigger function (case-insensitive).</param>
    /// <param name="changesJson">
    /// A JSON array string representing the batch of row changes.
    /// Each element must match the <c>SqlChange&lt;T&gt;</c> shape:
    /// <c>[{"operation":0,"item":{...}}, ...]</c> where operation is 0=Insert, 1=Update, 2=Delete.
    /// Must contain at least one element.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeSqlAsync(
        this IFunctionsTestHost host,
        string functionName,
        string changesJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changesJson);

        var context = new FunctionInvocationContext
        {
            TriggerType = "sqlTrigger",
            InputData = { ["$changesJson"] = changesJson }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    private static TriggerBindingData CreateBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$changesJson", out var j) ? j?.ToString() ?? "[]" : "[]";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }
}
