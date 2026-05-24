using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.DataExplorer;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// <c>[KustoInput]</c> binding support.
/// </summary>
public static class FunctionsTestHostBuilderDataExplorerExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Registers a single row to be injected for every function invocation that declares
    /// a <c>[KustoInput(Database = <paramref name="database"/>, KqlCommand = "...")]</c> parameter.
    /// </summary>
    /// <typeparam name="T">The row type. It is serialized to a single-element JSON array.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="database">
    /// The Kusto database name exactly as declared in the <c>[KustoInput]</c> attribute.
    /// </param>
    /// <param name="table">
    /// The Kusto table name that appears as the first table identifier in the
    /// <c>KqlCommand</c> query.
    /// </param>
    /// <param name="row">The row to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithKustoInputRows<T>(
        this IFunctionsTestHostBuilder builder,
        string database,
        string table,
        T row)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(database);
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentNullException.ThrowIfNull(row);

        var json = JsonSerializer.Serialize(new[] { row }, _jsonOptions);
        return builder.WithSyntheticBindingProvider(
            new KustoInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{database}/{table}"] = json
                }));
    }

    /// <summary>
    /// Registers a list of rows to be injected for every function invocation that declares
    /// a <c>[KustoInput(Database = <paramref name="database"/>, KqlCommand = "...")]</c> parameter.
    /// </summary>
    /// <typeparam name="T">The row type. Each element is serialized to camelCase JSON.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="database">
    /// The Kusto database name exactly as declared in the <c>[KustoInput]</c> attribute.
    /// </param>
    /// <param name="table">
    /// The Kusto table name that appears as the first table identifier in the
    /// <c>KqlCommand</c> query.
    /// </param>
    /// <param name="rows">The rows to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithKustoInputRows<T>(
        this IFunctionsTestHostBuilder builder,
        string database,
        string table,
        IReadOnlyList<T> rows)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(database);
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentNullException.ThrowIfNull(rows);

        var json = JsonSerializer.Serialize(rows, _jsonOptions);
        return builder.WithSyntheticBindingProvider(
            new KustoInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{database}/{table}"] = json
                }));
    }

    /// <summary>
    /// Registers pre-serialized JSON to be injected for every function invocation that declares
    /// a <c>[KustoInput(Database = <paramref name="database"/>, KqlCommand = "...")]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="database">
    /// The Kusto database name exactly as declared in the <c>[KustoInput]</c> attribute.
    /// </param>
    /// <param name="table">
    /// The Kusto table name that appears as the first table identifier in the
    /// <c>KqlCommand</c> query.
    /// </param>
    /// <param name="json">The JSON string to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithKustoInputJson(
        this IFunctionsTestHostBuilder builder,
        string database,
        string table,
        string json)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(database);
        ArgumentException.ThrowIfNullOrEmpty(table);
        ArgumentNullException.ThrowIfNull(json);

        return builder.WithSyntheticBindingProvider(
            new KustoInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{database}/{table}"] = json
                }));
    }
}
