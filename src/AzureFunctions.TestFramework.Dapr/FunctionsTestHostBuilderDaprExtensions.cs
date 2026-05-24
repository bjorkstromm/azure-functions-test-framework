using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Dapr;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// <c>[DaprStateInput]</c> and <c>[DaprSecretInput]</c> binding support.
/// </summary>
public static class FunctionsTestHostBuilderDaprExtensions
{
    // -------------------------------------------------------------------------
    // DaprStateInput
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a string value to be injected for every function invocation that declares
    /// a <c>[DaprStateInput(stateStore: <paramref name="stateStore"/>)]</c> parameter with
    /// <c>Key = <paramref name="key"/></c>.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="stateStore">
    /// The state store name exactly as declared in the <c>[DaprStateInput]</c> attribute
    /// (e.g. <c>"my-store"</c>). Matching is case-insensitive.
    /// </param>
    /// <param name="key">
    /// The key name exactly as declared in the <c>[DaprStateInput]</c> attribute's
    /// <c>Key</c> property (e.g. <c>"my-key"</c>). Matching is case-insensitive.
    /// </param>
    /// <param name="value">The string value to inject as the input binding result.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <strong>Note:</strong> The Azure Functions Worker SDK source generator (as of v2.0.7) does
    /// not emit <c>daprState</c> binding metadata for <c>[DaprStateInput]</c> parameters. This
    /// method has no effect in source-generated mode. Use <c>ConfigureServices</c> to inject fake
    /// state values instead.
    /// </remarks>
    public static IFunctionsTestHostBuilder WithDaprStateInput(
        this IFunctionsTestHostBuilder builder,
        string stateStore,
        string key,
        string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(stateStore);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        return builder.WithSyntheticBindingProvider(
            new DaprStateInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{stateStore}/{key}"] = value
                }));
    }

    /// <summary>
    /// Registers pre-serialized JSON to be injected for every function invocation that declares
    /// a <c>[DaprStateInput(stateStore: <paramref name="stateStore"/>)]</c> parameter with
    /// <c>Key = <paramref name="key"/></c>.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="stateStore">
    /// The state store name exactly as declared in the <c>[DaprStateInput]</c> attribute.
    /// Matching is case-insensitive.
    /// </param>
    /// <param name="key">
    /// The key name exactly as declared in the <c>[DaprStateInput]</c> attribute's <c>Key</c>
    /// property. Matching is case-insensitive.
    /// </param>
    /// <param name="json">The JSON string to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <strong>Note:</strong> The Azure Functions Worker SDK source generator (as of v2.0.7) does
    /// not emit <c>daprState</c> binding metadata for <c>[DaprStateInput]</c> parameters. This
    /// method has no effect in source-generated mode. Use <c>ConfigureServices</c> to inject fake
    /// state values instead.
    /// </remarks>
    public static IFunctionsTestHostBuilder WithDaprStateInputJson(
        this IFunctionsTestHostBuilder builder,
        string stateStore,
        string key,
        string json)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(stateStore);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(json);

        return builder.WithSyntheticBindingProvider(
            new DaprStateInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{stateStore}/{key}"] = json
                },
                isJson: true));
    }

    // -------------------------------------------------------------------------
    // DaprSecretInput
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a string value to be injected for every function invocation that declares
    /// a <c>[DaprSecretInput(secretStoreName: <paramref name="secretStoreName"/>, key: <paramref name="key"/>)]</c>
    /// parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="secretStoreName">
    /// The secret store name exactly as declared in the <c>[DaprSecretInput]</c> attribute's first
    /// constructor argument (e.g. <c>"my-secrets"</c>). Matching is case-insensitive.
    /// </param>
    /// <param name="key">
    /// The key exactly as declared in the <c>[DaprSecretInput]</c> attribute's second constructor
    /// argument (e.g. <c>"my-secret-key"</c>). Matching is case-insensitive.
    /// </param>
    /// <param name="value">The string value to inject as the input binding result.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <strong>Note:</strong> The Azure Functions Worker SDK source generator (as of v2.0.7) does
    /// not emit <c>daprSecret</c> binding metadata for <c>[DaprSecretInput]</c> parameters. This
    /// method has no effect in source-generated mode. Use <c>ConfigureServices</c> to inject fake
    /// secret values instead.
    /// </remarks>
    public static IFunctionsTestHostBuilder WithDaprSecretInput(
        this IFunctionsTestHostBuilder builder,
        string secretStoreName,
        string key,
        string value)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(secretStoreName);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        return builder.WithSyntheticBindingProvider(
            new DaprSecretInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{secretStoreName}/{key}"] = value
                }));
    }

    /// <summary>
    /// Registers pre-serialized JSON to be injected for every function invocation that declares
    /// a <c>[DaprSecretInput(secretStoreName: <paramref name="secretStoreName"/>, key: <paramref name="key"/>)]</c>
    /// parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="secretStoreName">
    /// The secret store name exactly as declared in the <c>[DaprSecretInput]</c> attribute's first
    /// constructor argument. Matching is case-insensitive.
    /// </param>
    /// <param name="key">
    /// The key exactly as declared in the <c>[DaprSecretInput]</c> attribute's second constructor
    /// argument. Matching is case-insensitive.
    /// </param>
    /// <param name="json">The JSON string to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <strong>Note:</strong> The Azure Functions Worker SDK source generator (as of v2.0.7) does
    /// not emit <c>daprSecret</c> binding metadata for <c>[DaprSecretInput]</c> parameters. This
    /// method has no effect in source-generated mode. Use <c>ConfigureServices</c> to inject fake
    /// secret values instead.
    /// </remarks>
    public static IFunctionsTestHostBuilder WithDaprSecretInputJson(
        this IFunctionsTestHostBuilder builder,
        string secretStoreName,
        string key,
        string json)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(secretStoreName);
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(json);

        return builder.WithSyntheticBindingProvider(
            new DaprSecretInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{secretStoreName}/{key}"] = json
                },
                isJson: true));
    }
}
