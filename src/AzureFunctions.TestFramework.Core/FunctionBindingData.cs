namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Describes a single parameter binding value for an Azure Function invocation.
/// Exactly one of <see cref="Bytes"/>, <see cref="Json"/>, or <see cref="StringValue"/>
/// should be set; the gRPC layer converts this to the appropriate <c>TypedData</c> variant.
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

    /// <summary>Creates a <see cref="FunctionBindingData"/> whose value is raw bytes.</summary>
    public static FunctionBindingData WithBytes(string name, byte[] bytes)
        => new() { Name = name, Bytes = bytes };

    /// <summary>Creates a <see cref="FunctionBindingData"/> whose value is a JSON string.</summary>
    public static FunctionBindingData WithJson(string name, string json)
        => new() { Name = name, Json = json };

    /// <summary>Creates a <see cref="FunctionBindingData"/> whose value is a plain string.</summary>
    public static FunctionBindingData WithString(string name, string value)
        => new() { Name = name, StringValue = value };
}
