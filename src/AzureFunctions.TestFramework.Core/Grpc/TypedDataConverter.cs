using Google.Protobuf;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Text.Json;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Converts gRPC <see cref="TypedData"/> values to their .NET equivalents.
/// </summary>
internal static class TypedDataConverter
{
    /// <summary>
    /// Converts a <see cref="TypedData"/> message to its corresponding .NET object.
    /// Returns <see langword="null"/> when <paramref name="data"/> is <see langword="null"/>
    /// or has <see cref="TypedData.DataOneofCase.None"/>.
    /// </summary>
    internal static object? Convert(TypedData? data)
    {
        if (data == null)
        {
            return null;
        }

        return data.DataCase switch
        {
            TypedData.DataOneofCase.None => null,
            TypedData.DataOneofCase.String => data.String,
            TypedData.DataOneofCase.Json => ParseJsonValue(data.Json),
            TypedData.DataOneofCase.Bytes => data.Bytes.ToByteArray(),
            TypedData.DataOneofCase.Stream => data.Stream.ToByteArray(),
            TypedData.DataOneofCase.Int => data.Int,
            TypedData.DataOneofCase.Double => data.Double,
            TypedData.DataOneofCase.CollectionString => data.CollectionString.String.ToArray(),
            TypedData.DataOneofCase.CollectionBytes => data.CollectionBytes.Bytes.Select(static b => b.ToByteArray()).ToArray(),
            TypedData.DataOneofCase.CollectionDouble => data.CollectionDouble.Double.ToArray(),
            TypedData.DataOneofCase.CollectionSint64 => data.CollectionSint64.Sint64.ToArray(),
            TypedData.DataOneofCase.ModelBindingData => ConvertModelBindingData(data.ModelBindingData),
            TypedData.DataOneofCase.CollectionModelBindingData => data.CollectionModelBindingData.ModelBindingData
                .Select(ConvertModelBindingData)
                .ToArray(),
            TypedData.DataOneofCase.Http => data.Http,
            _ => null
        };
    }

    internal static object? ParseJsonValue(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    internal static object ConvertModelBindingData(ModelBindingData data)
    {
        return new
        {
            data.Version,
            Source = data.Source,
            ContentType = data.ContentType,
            Content = data.Content.ToByteArray()
        };
    }
}
