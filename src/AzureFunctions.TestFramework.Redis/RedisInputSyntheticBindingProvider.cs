using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Redis;

/// <summary>
/// Injects synthetic <c>redis</c> input binding data into every invocation of functions that
/// declare a <c>[RedisInput]</c> parameter.
/// <para>
/// The real Azure Functions host executes the specified Redis command and passes the result as a
/// string in the <c>InputData</c> of the <c>InvocationRequest</c>. This provider injects a
/// pre-configured value so that the worker's Redis input converter can construct the target type
/// (e.g. <c>string</c>, <c>byte[]</c>, or any JSON-deserializable type).
/// </para>
/// <para>
/// The source-generated binding metadata for <c>[RedisInput]</c> uses type <c>"redis"</c>
/// with <c>direction: "In"</c>. This provider matches on <c>"redis"</c> and skips
/// output-direction bindings automatically.
/// </para>
/// <para>
/// Lookup is keyed by the <c>command</c> value declared in the <c>[RedisInput]</c> attribute
/// (case-insensitive). For example <c>"GET mykey"</c>.
/// </para>
/// <para>
/// Register via
/// <see cref="FunctionsTestHostBuilderRedisExtensions.WithRedisInput(IFunctionsTestHostBuilder, string, string)"/>
/// or
/// <see cref="FunctionsTestHostBuilderRedisExtensions.WithRedisInputJson(IFunctionsTestHostBuilder, string, string)"/>.
/// </para>
/// </summary>
public sealed class RedisInputSyntheticBindingProvider : ISyntheticBindingProvider
{
    // Key: command (case-insensitive), Value: value to inject
    private readonly IReadOnlyDictionary<string, string> _valueByCommand;
    private readonly bool _isJson;

    /// <summary>
    /// Initialises a new instance of <see cref="RedisInputSyntheticBindingProvider"/> with the
    /// specified command-to-value mappings.
    /// </summary>
    /// <param name="valueByCommand">
    /// A dictionary mapping Redis command strings (as declared in <c>[RedisInput]</c>) to the
    /// value to inject as the input binding result. Lookups are case-insensitive.
    /// </param>
    /// <param name="isJson">
    /// <see langword="true"/> if the values are pre-serialized JSON and should be injected via
    /// <see cref="FunctionBindingData.WithJson"/>; <see langword="false"/> (default) to inject as
    /// a plain string via <see cref="FunctionBindingData.WithString"/>.
    /// </param>
    public RedisInputSyntheticBindingProvider(
        IReadOnlyDictionary<string, string> valueByCommand,
        bool isJson = false)
    {
        ArgumentNullException.ThrowIfNull(valueByCommand);
        _valueByCommand = valueByCommand;
        _isJson = isJson;
    }

    /// <inheritdoc/>
    public string BindingType => "redis";

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

        var command = bindingConfig.TryGetProperty("command", out var cmd) ? cmd.GetString() : null;

        if (command is not null && _valueByCommand.TryGetValue(command, out var value))
        {
            return _isJson
                ? FunctionBindingData.WithJson(parameterName, value)
                : FunctionBindingData.WithString(parameterName, value);
        }

        // No registered value for this command — inject an empty string.
        return FunctionBindingData.WithString(parameterName, string.Empty);
    }
}
