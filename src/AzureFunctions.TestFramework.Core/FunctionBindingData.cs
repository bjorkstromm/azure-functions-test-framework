namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Describes a single parameter binding value for an Azure Function invocation.
/// Exactly one of <see cref="Bytes"/>, <see cref="Json"/>, <see cref="StringValue"/>,
/// <see cref="ModelBindingData"/>, or <see cref="CollectionModelBindingData"/> should be set;
/// the gRPC layer converts this to the appropriate <c>TypedData</c> variant.
/// </summary>
public sealed class FunctionBindingData
{
    /// <summary>Gets the parameter (binding) name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets raw bytes to pass as the binding value, mapped to <c>TypedData.Bytes</c>.</summary>
    public byte[]? Bytes { get; init; }

    /// <summary>Gets a JSON string to pass as the binding value, mapped to <c>TypedData.Json</c>.</summary>
    public string? Json { get; init; }

    /// <summary>Gets a plain string to pass as the binding value, mapped to <c>TypedData.String</c>.</summary>
    public string? StringValue { get; init; }

    /// <summary>
    /// Gets a structured model binding payload, mapped to <c>TypedData.ModelBindingData</c>.
    /// Used by extensions such as Service Bus to pass AMQP-encoded messages.
    /// </summary>
    public ModelBindingDataValue? ModelBindingData { get; init; }

    /// <summary>
    /// Gets a collection of structured model binding payloads, mapped to
    /// <c>TypedData.CollectionModelBindingData</c>.
    /// Used by extensions such as Service Bus batch triggers.
    /// </summary>
    public IReadOnlyList<ModelBindingDataValue>? CollectionModelBindingData { get; init; }

    /// <summary>Creates a <see cref="FunctionBindingData"/> whose value is raw bytes.</summary>
    public static FunctionBindingData WithBytes(string name, byte[] bytes)
        => new() { Name = name, Bytes = bytes };

    /// <summary>Creates a <see cref="FunctionBindingData"/> whose value is a JSON string.</summary>
    public static FunctionBindingData WithJson(string name, string json)
        => new() { Name = name, Json = json };

    /// <summary>Creates a <see cref="FunctionBindingData"/> whose value is a plain string.</summary>
    public static FunctionBindingData WithString(string name, string value)
        => new() { Name = name, StringValue = value };

    /// <summary>
    /// Creates a <see cref="FunctionBindingData"/> whose value is a single
    /// <see cref="ModelBindingDataValue"/>, mapped to <c>TypedData.ModelBindingData</c>.
    /// </summary>
    public static FunctionBindingData WithModelBindingData(string name, ModelBindingDataValue data)
        => new() { Name = name, ModelBindingData = data };

    /// <summary>
    /// Creates a <see cref="FunctionBindingData"/> whose value is a collection of
    /// <see cref="ModelBindingDataValue"/> items, mapped to
    /// <c>TypedData.CollectionModelBindingData</c>.
    /// </summary>
    public static FunctionBindingData WithCollectionModelBindingData(string name, IReadOnlyList<ModelBindingDataValue> items)
        => new() { Name = name, CollectionModelBindingData = items };
}
