using System.Text.Json;
using AzureFunctions.TestFramework.Core;

namespace AzureFunctions.TestFramework.CosmosDB;

/// <summary>
/// Extension methods on <see cref="IFunctionsTestHostBuilder"/> for configuring
/// <c>[CosmosDBInput]</c> binding support.
/// </summary>
public static class FunctionsTestHostBuilderCosmosDBExtensions
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Registers a single document to be injected for every function invocation that declares
    /// a <c>[CosmosDBInput(databaseName: <paramref name="databaseName"/>, containerName: <paramref name="containerName"/>)]</c> parameter.
    /// </summary>
    /// <typeparam name="T">The document type. It is serialized to JSON via camelCase.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="databaseName">
    /// The CosmosDB database name exactly as declared in the <c>[CosmosDBInput]</c> attribute.
    /// </param>
    /// <param name="containerName">
    /// The CosmosDB container name exactly as declared in the <c>[CosmosDBInput]</c> attribute.
    /// </param>
    /// <param name="document">The document to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithCosmosDBInputDocuments<T>(
        this IFunctionsTestHostBuilder builder,
        string databaseName,
        string containerName,
        T document)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(containerName);
        ArgumentNullException.ThrowIfNull(document);

        var json = JsonSerializer.Serialize(document, _jsonOptions);
        return builder.WithSyntheticBindingProvider(
            new CosmosDBInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{databaseName}/{containerName}"] = json
                }));
    }

    /// <summary>
    /// Registers a list of documents to be injected for every function invocation that declares
    /// a <c>[CosmosDBInput(databaseName: <paramref name="databaseName"/>, containerName: <paramref name="containerName"/>)]</c> parameter.
    /// </summary>
    /// <typeparam name="T">The document type. Each element is serialized to JSON via camelCase.</typeparam>
    /// <param name="builder">The test host builder.</param>
    /// <param name="databaseName">
    /// The CosmosDB database name exactly as declared in the <c>[CosmosDBInput]</c> attribute.
    /// </param>
    /// <param name="containerName">
    /// The CosmosDB container name exactly as declared in the <c>[CosmosDBInput]</c> attribute.
    /// </param>
    /// <param name="documents">The list of documents to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithCosmosDBInputDocuments<T>(
        this IFunctionsTestHostBuilder builder,
        string databaseName,
        string containerName,
        IReadOnlyList<T> documents)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(containerName);
        ArgumentNullException.ThrowIfNull(documents);

        var json = JsonSerializer.Serialize(documents, _jsonOptions);
        return builder.WithSyntheticBindingProvider(
            new CosmosDBInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{databaseName}/{containerName}"] = json
                }));
    }

    /// <summary>
    /// Registers pre-serialized JSON to be injected for every function invocation that declares
    /// a <c>[CosmosDBInput(databaseName: <paramref name="databaseName"/>, containerName: <paramref name="containerName"/>)]</c> parameter.
    /// </summary>
    /// <param name="builder">The test host builder.</param>
    /// <param name="databaseName">
    /// The CosmosDB database name exactly as declared in the <c>[CosmosDBInput]</c> attribute.
    /// </param>
    /// <param name="containerName">
    /// The CosmosDB container name exactly as declared in the <c>[CosmosDBInput]</c> attribute.
    /// </param>
    /// <param name="json">The JSON string to inject as the input binding value.</param>
    /// <returns>The builder for chaining.</returns>
    public static IFunctionsTestHostBuilder WithCosmosDBInputJson(
        this IFunctionsTestHostBuilder builder,
        string databaseName,
        string containerName,
        string json)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);
        ArgumentException.ThrowIfNullOrEmpty(containerName);
        ArgumentNullException.ThrowIfNull(json);

        return builder.WithSyntheticBindingProvider(
            new CosmosDBInputSyntheticBindingProvider(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [$"{databaseName}/{containerName}"] = json
                }));
    }
}
