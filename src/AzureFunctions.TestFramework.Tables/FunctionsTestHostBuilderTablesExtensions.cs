using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.Tables;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// <c>[TableInput]</c> binding support.
/// </summary>
public static class FunctionsTestHostBuilderTablesExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = null  // preserve property names as declared on the type
    };

    /// <summary>
    /// Registers a single fake table entity to be injected for every function invocation that
    /// declares a <c>[TableInput(<paramref name="tableName"/>, <paramref name="partitionKey"/>,
    /// <paramref name="rowKey"/>)]</c> parameter.
    /// </summary>
    /// <typeparam name="T">The entity type. Must be JSON-serializable.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="tableName">
    /// The table name exactly as declared in the <c>[TableInput]</c> attribute.
    /// </param>
    /// <param name="partitionKey">
    /// The partition key exactly as declared in the <c>[TableInput]</c> attribute.
    /// </param>
    /// <param name="rowKey">
    /// The row key exactly as declared in the <c>[TableInput]</c> attribute.
    /// </param>
    /// <param name="entity">The entity to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithTableEntity<T>(
        this IFunctionsTestHostBuilder builder,
        string tableName,
        string partitionKey,
        string rowKey,
        T entity)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentException.ThrowIfNullOrEmpty(partitionKey);
        ArgumentException.ThrowIfNullOrEmpty(rowKey);
        ArgumentNullException.ThrowIfNull(entity);

        var key = $"{tableName}/{partitionKey}/{rowKey}";
        var json = JsonSerializer.Serialize(entity, _jsonOptions);

        return builder.WithSyntheticBindingProvider(
            new TableInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [key] = json
                }));
    }

    /// <summary>
    /// Registers a collection of fake table entities to be injected for every function invocation
    /// that declares a <c>[TableInput(<paramref name="tableName"/>)]</c> parameter (full-table
    /// scan, no partition/row key filter).
    /// </summary>
    /// <typeparam name="T">The entity type. Must be JSON-serializable.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="tableName">
    /// The table name exactly as declared in the <c>[TableInput]</c> attribute.
    /// </param>
    /// <param name="entities">The entities to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithTableEntities<T>(
        this IFunctionsTestHostBuilder builder,
        string tableName,
        IEnumerable<T> entities)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentNullException.ThrowIfNull(entities);

        var json = JsonSerializer.Serialize(entities.ToList(), _jsonOptions);

        return builder.WithSyntheticBindingProvider(
            new TableInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [tableName] = json
                }));
    }

    /// <summary>
    /// Registers a partition-scoped collection of fake table entities to be injected for every
    /// function invocation that declares a
    /// <c>[TableInput(<paramref name="tableName"/>, <paramref name="partitionKey"/>)]</c>
    /// parameter.
    /// </summary>
    /// <typeparam name="T">The entity type. Must be JSON-serializable.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="tableName">
    /// The table name exactly as declared in the <c>[TableInput]</c> attribute.
    /// </param>
    /// <param name="partitionKey">
    /// The partition key exactly as declared in the <c>[TableInput]</c> attribute.
    /// </param>
    /// <param name="entities">The entities to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithTableEntities<T>(
        this IFunctionsTestHostBuilder builder,
        string tableName,
        string partitionKey,
        IEnumerable<T> entities)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentException.ThrowIfNullOrEmpty(partitionKey);
        ArgumentNullException.ThrowIfNull(entities);

        var key = $"{tableName}/{partitionKey}";
        var json = JsonSerializer.Serialize(entities.ToList(), _jsonOptions);

        return builder.WithSyntheticBindingProvider(
            new TableInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [key] = json
                }));
    }

    /// <summary>
    /// Registers multiple pre-configured table data entries (all key formats supported) to be
    /// injected for <c>[TableInput]</c> bindings.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="jsonByKey">
    /// A dictionary mapping lookup keys to JSON strings.
    /// Keys use the format <c>"tableName"</c>, <c>"tableName/partitionKey"</c>, or
    /// <c>"tableName/partitionKey/rowKey"</c> (case-insensitive).
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithTableInputData(
        this IFunctionsTestHostBuilder builder,
        IReadOnlyDictionary<string, string> jsonByKey)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(jsonByKey);

        return builder.WithSyntheticBindingProvider(new TableInputSyntheticBindingProvider(jsonByKey));
    }
}
