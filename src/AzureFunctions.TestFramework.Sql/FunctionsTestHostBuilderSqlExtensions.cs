using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Sql;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// <c>[SqlInput]</c> binding support.
/// </summary>
public static class FunctionsTestHostBuilderSqlExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Registers a single row to be injected for every function invocation that declares
    /// a <c>[SqlInput(commandText: <paramref name="commandText"/>)]</c> parameter.
    /// </summary>
    /// <typeparam name="T">The row type. It is serialized to a single-element JSON array.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="commandText">
    /// The SQL query or table name exactly as declared in the <c>[SqlInput]</c> attribute.
    /// </param>
    /// <param name="row">The row to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithSqlInputRows<T>(
        this IFunctionsTestHostBuilder builder,
        string commandText,
        T row)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(commandText);
        ArgumentNullException.ThrowIfNull(row);

        var json = JsonSerializer.Serialize(new[] { row }, _jsonOptions);
        return builder.WithSyntheticBindingProvider(
            new SqlInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [commandText] = json
                }));
    }

    /// <summary>
    /// Registers a list of rows to be injected for every function invocation that declares
    /// a <c>[SqlInput(commandText: <paramref name="commandText"/>)]</c> parameter.
    /// </summary>
    /// <typeparam name="T">The row type. Each element is serialized to camelCase JSON.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="commandText">
    /// The SQL query or table name exactly as declared in the <c>[SqlInput]</c> attribute.
    /// </param>
    /// <param name="rows">The list of rows to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithSqlInputRows<T>(
        this IFunctionsTestHostBuilder builder,
        string commandText,
        IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(commandText);
        ArgumentNullException.ThrowIfNull(rows);

        var json = JsonSerializer.Serialize(rows, _jsonOptions);
        return builder.WithSyntheticBindingProvider(
            new SqlInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [commandText] = json
                }));
    }

    /// <summary>
    /// Registers pre-serialized JSON to be injected for every function invocation that declares
    /// a <c>[SqlInput(commandText: <paramref name="commandText"/>)]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="commandText">
    /// The SQL query or table name exactly as declared in the <c>[SqlInput]</c> attribute.
    /// </param>
    /// <param name="json">The JSON array string to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithSqlInputJson(
        this IFunctionsTestHostBuilder builder,
        string commandText,
        string json)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(commandText);
        ArgumentNullException.ThrowIfNull(json);

        return builder.WithSyntheticBindingProvider(
            new SqlInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [commandText] = json
                }));
    }
}
