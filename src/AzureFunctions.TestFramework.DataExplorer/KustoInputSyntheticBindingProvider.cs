using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.DataExplorer;

/// <summary>
/// Injects synthetic <c>kusto</c> input binding data into every invocation of functions that
/// declare a <c>[KustoInput]</c> parameter.
/// <para>
/// The real Azure Functions host executes the Kusto query and passes the results as JSON in the
/// <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects pre-configured JSON
/// so that the worker's Kusto input converters can construct the target type.
/// </para>
/// <para>
/// The source-generated binding metadata for <c>[KustoInput]</c> uses type <c>"kusto"</c>
/// with <c>direction: "In"</c>. This provider matches on <c>"kusto"</c> and skips
/// output-direction bindings automatically.
/// </para>
/// <para>
/// Lookup is keyed by <c>"{database}/{table}"</c> (case-insensitive), where <c>table</c> is
/// derived from the first table identifier in <c>kqlCommand</c>.
/// </para>
/// </summary>
public sealed class KustoInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    // Key: "{database}/{table}" (case-insensitive), Value: pre-serialized JSON rows
    private readonly IReadOnlyDictionary<string, string> _rowsJsonByKey;

    /// <summary>
    /// Initializes a new instance of <see cref="KustoInputSyntheticBindingProvider"/> with the
    /// specified database/table-to-JSON mappings.
    /// </summary>
    /// <param name="rowsJsonByKey">
    /// A dictionary mapping <c>"{database}/{table}"</c> keys to the JSON string to inject
    /// as the input binding value. Lookups are case-insensitive.
    /// </param>
    public KustoInputSyntheticBindingProvider(IReadOnlyDictionary<string, string> rowsJsonByKey)
    {
        ArgumentNullException.ThrowIfNull(rowsJsonByKey);
        _rowsJsonByKey = rowsJsonByKey;
    }

    /// <inheritdoc/>
    public string BindingType => "kusto";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
    {
        // Only inject data for input bindings (direction "In").
        var direction = bindingConfig.TryGetProperty("direction", out var dir) ? dir.GetString() : null;
        if (direction is not null &&
            !direction.Equals("In", StringComparison.OrdinalIgnoreCase))
        {
            return FunctionBindingData.WithJson(parameterName, "null");
        }

        var database = bindingConfig.TryGetProperty("database", out var db) ? db.GetString() : null;
        var kqlCommand = bindingConfig.TryGetProperty("kqlCommand", out var cmd) ? cmd.GetString() : null;
        var table = TryExtractTableName(kqlCommand);

        if (database is not null && table is not null)
        {
            var key = $"{database}/{table}";
            if (_rowsJsonByKey.TryGetValue(key, out var json))
            {
                return FunctionBindingData.WithJson(parameterName, json);
            }
        }

        // No registered content for this binding — inject JSON null.
        return FunctionBindingData.WithJson(parameterName, "null");
    }

    private static string? TryExtractTableName(string? kqlCommand)
    {
        if (string.IsNullOrWhiteSpace(kqlCommand))
        {
            return null;
        }

        var command = kqlCommand.Trim();
        var pipeIndex = command.IndexOf('|');
        if (pipeIndex >= 0)
        {
            command = command[..pipeIndex].Trim();
        }

        if (command.Length == 0)
        {
            return null;
        }

        if (command[0] is '[')
        {
            var closeBracket = command.IndexOf(']');
            if (closeBracket > 1)
            {
                return command[1..closeBracket].Trim();
            }
        }

        var separators = new[] { ' ', '\t', '\r', '\n', '(' };
        var end = command.IndexOfAny(separators);
        return end > 0 ? command[..end].Trim() : command;
    }
}
