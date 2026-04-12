using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.CosmosDB;

/// <summary>
/// Extension methods for invoking CosmosDB-triggered Azure Functions via <see cref="IFunctionsTestHost"/>.
/// </summary>
public static class FunctionsTestHostCosmosDBExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Invokes a CosmosDB-triggered function by name with the specified list of changed documents.
    /// </summary>
    /// <typeparam name="T">
    /// The document type. Each element is serialized to JSON; the worker deserializes the resulting
    /// JSON array into the function parameter (e.g. <c>IReadOnlyList&lt;T&gt;</c> or <c>string</c>).
    /// </typeparam>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the CosmosDB trigger function (case-insensitive).</param>
    /// <param name="documents">
    /// The batch of changed documents to simulate as the CosmosDB change-feed trigger input.
    /// Must contain at least one document.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeCosmosDBAsync<T>(
        this IFunctionsTestHost host,
        string functionName,
        IReadOnlyList<T> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0)
            throw new ArgumentException("The document list must contain at least one document.", nameof(documents));

        var jsonArray = JsonSerializer.Serialize(documents, _jsonOptions);

        var context = new FunctionInvocationContext
        {
            TriggerType = "cosmosDBTrigger",
            InputData = { ["$documentsJson"] = jsonArray }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    /// <summary>
    /// Invokes a CosmosDB-triggered function by name with the specified list of raw JSON documents.
    /// </summary>
    /// <param name="host">The test host.</param>
    /// <param name="functionName">The name of the CosmosDB trigger function (case-insensitive).</param>
    /// <param name="documentsJson">
    /// A JSON array string representing the batch of changed documents.
    /// Must be a valid JSON array containing at least one element.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The invocation result.</returns>
    public static Task<FunctionInvocationResult> InvokeCosmosDBAsync(
        this IFunctionsTestHost host,
        string functionName,
        string documentsJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentsJson);

        var context = new FunctionInvocationContext
        {
            TriggerType = "cosmosDBTrigger",
            InputData = { ["$documentsJson"] = documentsJson }
        };

        return host.Invoker.InvokeAsync(functionName, context, CreateBindingData, cancellationToken);
    }

    private static TriggerBindingData CreateBindingData(
        FunctionInvocationContext context,
        FunctionRegistration function)
    {
        var json = context.InputData.TryGetValue("$documentsJson", out var j) ? j?.ToString() ?? "[]" : "[]";

        return new TriggerBindingData
        {
            InputData = [FunctionBindingData.WithJson(function.ParameterName, json)]
        };
    }
}
