using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Tables;

/// <summary>
/// Injects synthetic <c>table</c> input binding data into every invocation of functions that
/// declare a <c>[TableInput]</c> parameter.
/// <para>
/// The real Azure Functions host reads entity data from Azure Table Storage and passes it as JSON
/// in the <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects pre-configured
/// JSON so that the worker's table input converters can construct the target type
/// (e.g. a POCO, <c>TableEntity</c>, or <c>IEnumerable&lt;T&gt;</c>).
/// </para>
/// <para>
/// The source-generated binding metadata for <c>[TableInput]</c> uses type <c>"table"</c> with
/// <c>direction: "In"</c>. This provider matches on <c>"table"</c> and skips output-direction
/// bindings automatically.
/// </para>
/// <para>
/// Lookup is performed from most-specific to least-specific key:
/// <list type="number">
///   <item><c>tableName/partitionKey/rowKey</c> — exact single-entity match</item>
///   <item><c>tableName/partitionKey</c> — partition-scoped collection</item>
///   <item><c>tableName</c> — full-table collection</item>
/// </list>
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderTablesExtensions.WithTableEntity{T}(IFunctionsTestHostBuilder, string, string, string, T)"/>
/// or
/// <see cref="FunctionsTestHostBuilderTablesExtensions.WithTableEntities{T}(IFunctionsTestHostBuilder, string, IEnumerable{T})"/>.
/// </para>
/// </summary>
public sealed class TableInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = null  // use property names as-is
    };

    private readonly IReadOnlyDictionary<string, string> _jsonByKey;

    /// <summary>
    /// Initialises a new instance of <see cref="TableInputSyntheticBindingProvider"/> with the
    /// specified key-to-JSON mappings.
    /// </summary>
    /// <param name="jsonByKey">
    /// A dictionary mapping lookup keys to JSON strings to inject.
    /// Keys use the format <c>"tableName"</c>, <c>"tableName/partitionKey"</c>, or
    /// <c>"tableName/partitionKey/rowKey"</c> (case-insensitive).
    /// </param>
    public TableInputSyntheticBindingProvider(IReadOnlyDictionary<string, string> jsonByKey)
    {
        ArgumentNullException.ThrowIfNull(jsonByKey);
        _jsonByKey = jsonByKey;
    }

    /// <inheritdoc/>
    public string BindingType => "table";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
    {
        // Only inject data for input bindings (direction "In").
        // Output bindings (direction "Out"/"Return") do not need synthetic input data.
        var direction = bindingConfig.TryGetProperty("direction", out var dir) ? dir.GetString() : null;
        if (direction is not null &&
            !direction.Equals("In", StringComparison.OrdinalIgnoreCase))
        {
            // Return empty JSON for output/return bindings — the worker ignores extra InputData
            // entries for non-input parameters, so this is safe.
            return FunctionBindingData.WithJson(parameterName, "{}");
        }

        var tableName = bindingConfig.TryGetProperty("tableName", out var tn) ? tn.GetString() : null;
        var partitionKey = bindingConfig.TryGetProperty("partitionKey", out var pk) ? pk.GetString() : null;
        var rowKey = bindingConfig.TryGetProperty("rowKey", out var rk) ? rk.GetString() : null;

        if (tableName is null)
        {
            return FunctionBindingData.WithJson(parameterName, "{}");
        }

        // Try from most-specific key to least-specific.
        if (!string.IsNullOrEmpty(partitionKey) && !string.IsNullOrEmpty(rowKey))
        {
            var exactKey = $"{tableName}/{partitionKey}/{rowKey}";
            if (_jsonByKey.TryGetValue(exactKey, out var exactJson))
                return FunctionBindingData.WithJson(parameterName, exactJson);
        }

        if (!string.IsNullOrEmpty(partitionKey))
        {
            var partitionKey2 = $"{tableName}/{partitionKey}";
            if (_jsonByKey.TryGetValue(partitionKey2, out var partitionJson))
                return FunctionBindingData.WithJson(parameterName, partitionJson);
        }

        if (_jsonByKey.TryGetValue(tableName, out var tableJson))
            return FunctionBindingData.WithJson(parameterName, tableJson);

        // No registered content — inject an empty object (single entity) or empty array (collection).
        // The worker will surface this as a default-constructed entity or empty collection.
        return FunctionBindingData.WithJson(parameterName, "{}");
    }
}
