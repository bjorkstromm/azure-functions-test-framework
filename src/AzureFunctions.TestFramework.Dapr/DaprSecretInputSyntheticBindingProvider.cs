using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Dapr;

/// <summary>
/// Injects synthetic <c>daprSecret</c> input binding data into every invocation of functions that
/// declare a <c>[DaprSecretInput]</c> parameter.
/// <para>
/// The real Azure Functions host retrieves a secret from the Dapr secret store and passes it as a
/// value in the <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects a
/// pre-configured value so that the worker's Dapr secret converter can construct the target type
/// (e.g. <c>string</c>, <c>IDictionary&lt;string, string&gt;</c>, or a JSON-deserializable type).
/// </para>
/// <para>
/// When <c>daprSecret</c> binding metadata is present (for example in <c>function.json</c>), it
/// uses type <c>"daprSecret"</c> with <c>direction: "In"</c>. This provider matches on
/// <c>"daprSecret"</c> and ignores non-input bindings automatically.
/// </para>
/// <para>
/// Lookup is keyed by <c>"{secretStoreName}/{key}"</c> (case-insensitive). For example
/// <c>"my-secrets/my-key"</c>.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderDaprExtensions.WithDaprSecretInput(IFunctionsTestHostBuilder, string, string, string)"/>
/// or
/// <see cref="FunctionsTestHostBuilderDaprExtensions.WithDaprSecretInputJson(IFunctionsTestHostBuilder, string, string, string)"/>.
/// </para>
/// <remarks>
/// <strong>Note:</strong> The Azure Functions Worker SDK source generator (as of v2.0.7) does not
/// emit <c>daprSecret</c> binding metadata for <c>[DaprSecretInput]</c> parameters, so this
/// provider is only invoked when the binding metadata is present in <c>function.json</c>. In
/// source-generated mode, inject secret values via <c>ConfigureServices</c> instead.
/// </remarks>
/// </summary>
public sealed class DaprSecretInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    // Key: "{secretStoreName}/{key}" (case-insensitive), Value: value to inject
    private readonly IReadOnlyDictionary<string, string> _valueByKey;
    private readonly bool _isJson;

    /// <summary>
    /// Initialises a new instance of <see cref="DaprSecretInputSyntheticBindingProvider"/> with the
    /// specified secret store/key-to-value mappings.
    /// </summary>
    /// <param name="valueByKey">
    /// A dictionary mapping <c>"{secretStoreName}/{key}"</c> strings to the value to inject as the
    /// input binding result. Lookups are case-insensitive.
    /// </param>
    /// <param name="isJson">
    /// <see langword="true"/> if the values are pre-serialized JSON and should be injected via
    /// <see cref="FunctionBindingData.WithJson"/>; <see langword="false"/> (default) to inject as
    /// a plain string via <see cref="FunctionBindingData.WithString"/>.
    /// </param>
    public DaprSecretInputSyntheticBindingProvider(
        IReadOnlyDictionary<string, string> valueByKey,
        bool isJson = false)
    {
        ArgumentNullException.ThrowIfNull(valueByKey);
        _valueByKey = valueByKey;
        _isJson = isJson;
    }

    /// <inheritdoc/>
    public string BindingType => "daprSecret";

    /// <inheritdoc/>
    public FunctionBindingData? CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
    {
        // Only inject data for input bindings (direction "In"). Return null to skip.
        var direction = bindingConfig.TryGetProperty("direction", out var dir) ? dir.GetString() : null;
        if (direction is not null &&
            !direction.Equals("In", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var secretStoreName = bindingConfig.TryGetProperty("secretStoreName", out var ssn) ? ssn.GetString() : null;
        var key = bindingConfig.TryGetProperty("key", out var k) ? k.GetString() : null;

        if (secretStoreName is not null && key is not null)
        {
            var lookupKey = $"{secretStoreName}/{key}";
            if (_valueByKey.TryGetValue(lookupKey, out var value))
            {
                return _isJson
                    ? FunctionBindingData.WithJson(parameterName, value)
                    : FunctionBindingData.WithString(parameterName, value);
            }
        }

        // No registered value — skip injection.
        return null;
    }
}
