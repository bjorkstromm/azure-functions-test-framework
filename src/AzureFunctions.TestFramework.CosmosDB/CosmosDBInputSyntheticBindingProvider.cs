using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.CosmosDB;

/// <summary>
/// Injects synthetic <c>cosmosDB</c> input binding data into every invocation of functions that
/// declare a <c>[CosmosDBInput]</c> parameter.
/// <para>
/// The real Azure Functions host reads documents from CosmosDB and passes them as JSON in the
/// <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects pre-configured
/// JSON so that the worker's CosmosDB input converters can construct the target type
/// (e.g. a model type, <c>string</c>, or <c>IEnumerable&lt;T&gt;</c>).
/// </para>
/// <para>
/// The source-generated binding metadata for <c>[CosmosDBInput]</c> uses type <c>"cosmosDB"</c>
/// with <c>direction: "In"</c>. This provider matches on <c>"cosmosDB"</c> and skips
/// output-direction bindings automatically.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderCosmosDBExtensions.WithCosmosDBInputDocuments{T}(IFunctionsTestHostBuilder, string, string, T)"/>
/// or
/// <see cref="FunctionsTestHostBuilderCosmosDBExtensions.WithCosmosDBInputDocuments{T}(IFunctionsTestHostBuilder, string, string, IReadOnlyList{T})"/>.
/// </para>
/// </summary>
public sealed class CosmosDBInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Key: "{databaseName}/{containerName}" (case-insensitive), Value: pre-serialized JSON
    private readonly IReadOnlyDictionary<string, string> _documentJsonByKey;

    /// <summary>
    /// Initialises a new instance of <see cref="CosmosDBInputSyntheticBindingProvider"/> with the
    /// specified database/container-to-JSON mappings.
    /// </summary>
    /// <param name="documentJsonByKey">
    /// A dictionary mapping <c>"{databaseName}/{containerName}"</c> keys to the JSON string to inject
    /// as the input binding value. Lookups are case-insensitive.
    /// </param>
    public CosmosDBInputSyntheticBindingProvider(IReadOnlyDictionary<string, string> documentJsonByKey)
    {
        ArgumentNullException.ThrowIfNull(documentJsonByKey);
        _documentJsonByKey = documentJsonByKey;
    }

    /// <inheritdoc/>
    public string BindingType => "cosmosDB";

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

        var databaseName = bindingConfig.TryGetProperty("databaseName", out var db) ? db.GetString() : null;
        var containerName = bindingConfig.TryGetProperty("containerName", out var cn) ? cn.GetString() : null;

        if (databaseName is not null && containerName is not null)
        {
            var key = $"{databaseName}/{containerName}";
            if (_documentJsonByKey.TryGetValue(key, out var json))
            {
                return FunctionBindingData.WithJson(parameterName, json);
            }
        }

        // No registered content for this binding — inject JSON null.
        return FunctionBindingData.WithJson(parameterName, "null");
    }
}
