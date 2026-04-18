using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Sql;

/// <summary>
/// Injects synthetic <c>sql</c> input binding data into every invocation of functions that
/// declare a <c>[SqlInput]</c> parameter.
/// <para>
/// The real Azure Functions host executes the SQL query and passes the result rows as JSON in
/// the <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects pre-configured
/// JSON so that the worker's SQL input converters can construct the target type
/// (e.g. a model type, <c>string</c>, or <c>IEnumerable&lt;T&gt;</c>).
/// </para>
/// <para>
/// The source-generated binding metadata for <c>[SqlInput]</c> uses type <c>"sql"</c>
/// with <c>direction: "In"</c>. This provider matches on <c>"sql"</c> and skips
/// output-direction bindings automatically.
/// </para>
/// <para>
/// Lookup is keyed by the <c>commandText</c> value declared in the <c>[SqlInput]</c> attribute
/// (case-insensitive).
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderSqlExtensions.WithSqlInputRows{T}(IFunctionsTestHostBuilder, string, IReadOnlyList{T})"/>.
/// </para>
/// </summary>
public sealed class SqlInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Key: commandText (case-insensitive), Value: pre-serialized JSON array
    private readonly IReadOnlyDictionary<string, string> _jsonByCommandText;

    /// <summary>
    /// Initialises a new instance of <see cref="SqlInputSyntheticBindingProvider"/> with the
    /// specified commandText-to-JSON mappings.
    /// </summary>
    /// <param name="jsonByCommandText">
    /// A dictionary mapping <c>commandText</c> values (as declared in <c>[SqlInput]</c>) to
    /// the JSON string to inject as the input binding value. Lookups are case-insensitive.
    /// </param>
    public SqlInputSyntheticBindingProvider(IReadOnlyDictionary<string, string> jsonByCommandText)
    {
        ArgumentNullException.ThrowIfNull(jsonByCommandText);
        _jsonByCommandText = jsonByCommandText;
    }

    /// <inheritdoc/>
    public string BindingType => "sql";

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

        var commandText = bindingConfig.TryGetProperty("commandText", out var ct) ? ct.GetString() : null;

        if (commandText is not null &&
            _jsonByCommandText.TryGetValue(commandText, out var json))
        {
            return FunctionBindingData.WithJson(parameterName, json);
        }

        // No registered content for this binding — inject JSON null.
        return FunctionBindingData.WithJson(parameterName, "null");
    }
}
