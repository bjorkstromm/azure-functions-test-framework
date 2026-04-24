using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Dapr;

/// <summary>
/// Injects synthetic <c>daprState</c> input binding data into every invocation of functions that
/// declare a <c>[DaprStateInput]</c> parameter.
/// <para>
/// The real Azure Functions host retrieves state from the Dapr state store and passes it as a
/// value in the <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects a
/// pre-configured value so that the worker's Dapr state converter can construct the target type
/// (e.g. <c>string</c>, or any JSON-deserializable type).
/// </para>
/// <para>
/// The source-generated binding metadata for <c>[DaprStateInput]</c> uses type <c>"daprState"</c>
/// with <c>direction: "In"</c>. This provider matches on <c>"daprState"</c> and skips
/// output-direction bindings automatically.
/// </para>
/// <para>
/// Lookup is keyed by <c>"{stateStore}/{key}"</c> (case-insensitive). For example
/// <c>"my-store/my-key"</c>.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderDaprExtensions.WithDaprStateInput(IFunctionsTestHostBuilder, string, string, string)"/>
/// or
/// <see cref="FunctionsTestHostBuilderDaprExtensions.WithDaprStateInputJson(IFunctionsTestHostBuilder, string, string, string)"/>.
/// </para>
/// <remarks>
/// <strong>Note:</strong> The Azure Functions Worker SDK source generator (as of v2.0.7) does not
/// emit <c>daprState</c> binding metadata for <c>[DaprStateInput]</c> parameters, so this
/// provider is only invoked when the binding metadata is present in <c>function.json</c>. In
/// source-generated mode, inject state values via <c>ConfigureServices</c> instead.
/// </remarks>
/// </summary>
public sealed class DaprStateInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    // Key: "{stateStore}/{key}" (case-insensitive), Value: value to inject
    private readonly IReadOnlyDictionary<string, string> _valueByKey;
    private readonly bool _isJson;

    /// <summary>
    /// Initialises a new instance of <see cref="DaprStateInputSyntheticBindingProvider"/> with the
    /// specified state store/key-to-value mappings.
    /// </summary>
    /// <param name="valueByKey">
    /// A dictionary mapping <c>"{stateStore}/{key}"</c> strings to the value to inject as the
    /// input binding result. Lookups are case-insensitive.
    /// </param>
    /// <param name="isJson">
    /// <see langword="true"/> if the values are pre-serialized JSON and should be injected via
    /// <see cref="FunctionBindingData.WithJson"/>; <see langword="false"/> (default) to inject as
    /// a plain string via <see cref="FunctionBindingData.WithString"/>.
    /// </param>
    public DaprStateInputSyntheticBindingProvider(
        IReadOnlyDictionary<string, string> valueByKey,
        bool isJson = false)
    {
        ArgumentNullException.ThrowIfNull(valueByKey);
        _valueByKey = valueByKey;
        _isJson = isJson;
    }

    /// <inheritdoc/>
    public string BindingType => "daprState";

    /// <inheritdoc/>
    public FunctionBindingData CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
    {
        // Only inject data for input bindings (direction "In").
        var direction = bindingConfig.TryGetProperty("direction", out var dir) ? dir.GetString() : null;
        if (direction is not null &&
            !direction.Equals("In", StringComparison.OrdinalIgnoreCase))
        {
            return FunctionBindingData.WithString(parameterName, string.Empty);
        }

        var stateStore = bindingConfig.TryGetProperty("stateStore", out var ss) ? ss.GetString() : null;
        var key = bindingConfig.TryGetProperty("key", out var k) ? k.GetString() : null;

        if (stateStore is not null && key is not null)
        {
            var lookupKey = $"{stateStore}/{key}";
            if (_valueByKey.TryGetValue(lookupKey, out var value))
            {
                return _isJson
                    ? FunctionBindingData.WithJson(parameterName, value)
                    : FunctionBindingData.WithString(parameterName, value);
            }
        }

        // No registered value — inject an empty string.
        return FunctionBindingData.WithString(parameterName, string.Empty);
    }
}
