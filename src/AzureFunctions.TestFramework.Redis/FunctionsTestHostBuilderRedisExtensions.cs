using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Redis;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// <c>[RedisInput]</c> binding support.
/// </summary>
public static class FunctionsTestHostBuilderRedisExtensions
{
    /// <summary>
    /// Registers a string value to be injected for every function invocation that declares
    /// a <c>[RedisInput(command: <paramref name="command"/>)]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="command">
    /// The full Redis command exactly as declared in the <c>[RedisInput]</c> attribute's
    /// <c>command</c> argument (e.g. <c>"GET mykey"</c>). Matching is case-insensitive.
    /// </param>
    /// <param name="value">The string value to inject as the input binding result.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithRedisInput(
        this IFunctionsTestHostBuilder builder,
        string command,
        string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(command);
        ArgumentNullException.ThrowIfNull(value);

        return builder.WithSyntheticBindingProvider(
            new RedisInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [command] = value
                }));
    }

    /// <summary>
    /// Registers pre-serialized JSON to be injected for every function invocation that declares
    /// a <c>[RedisInput(command: <paramref name="command"/>)]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="command">
    /// The full Redis command exactly as declared in the <c>[RedisInput]</c> attribute's
    /// <c>command</c> argument (e.g. <c>"GET mykey"</c>). Matching is case-insensitive.
    /// </param>
    /// <param name="json">The JSON string to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithRedisInputJson(
        this IFunctionsTestHostBuilder builder,
        string command,
        string json)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(command);
        ArgumentNullException.ThrowIfNull(json);

        return builder.WithSyntheticBindingProvider(
            new RedisInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [command] = json
                },
                isJson: true));
    }
}
