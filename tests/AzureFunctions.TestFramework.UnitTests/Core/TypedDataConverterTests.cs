using AzureFunctions.TestFramework.Core.Grpc;
using Google.Protobuf;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.UnitTests.Core;

/// <summary>
/// Unit tests for <see cref="TypedDataConverter.Convert"/>.
/// </summary>
public class TypedDataConverterTests
{
    [Fact]
    public void Convert_Null_ReturnsNull()
    {
        var result = TypedDataConverter.Convert(null);
        Assert.Null(result);
    }

    [Fact]
    public void Convert_None_ReturnsNull()
    {
        var data = new TypedData(); // DataCase == None
        var result = TypedDataConverter.Convert(data);
        Assert.Null(result);
    }

    [Fact]
    public void Convert_String_ReturnsString()
    {
        var data = new TypedData { String = "hello" };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Convert_Json_ReturnsJsonElement()
    {
        var data = new TypedData { Json = """{"key":"value"}""" };
        var result = TypedDataConverter.Convert(data);
        Assert.IsType<JsonElement>(result);
        var element = (JsonElement)result;
        Assert.Equal("value", element.GetProperty("key").GetString());
    }

    [Fact]
    public void Convert_Json_EmptyString_ReturnsNull()
    {
        var data = new TypedData { Json = "" };
        var result = TypedDataConverter.Convert(data);
        Assert.Null(result);
    }

    [Fact]
    public void Convert_Json_WhitespaceOnly_ReturnsNull()
    {
        var data = new TypedData { Json = "   " };
        var result = TypedDataConverter.Convert(data);
        Assert.Null(result);
    }

    [Fact]
    public void Convert_Bytes_ReturnsByteArray()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var data = new TypedData { Bytes = ByteString.CopyFrom(bytes) };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal(bytes, result);
    }

    [Fact]
    public void Convert_Stream_ReturnsByteArray()
    {
        var bytes = new byte[] { 4, 5, 6 };
        var data = new TypedData { Stream = ByteString.CopyFrom(bytes) };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal(bytes, result);
    }

    [Fact]
    public void Convert_Int_ReturnsLong()
    {
        var data = new TypedData { Int = 42L };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal(42L, result);
    }

    [Fact]
    public void Convert_Double_ReturnsDouble()
    {
        var data = new TypedData { Double = 3.14 };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void Convert_CollectionString_ReturnsStringArray()
    {
        var col = new CollectionString();
        col.String.Add("a");
        col.String.Add("b");
        var data = new TypedData { CollectionString = col };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal(new[] { "a", "b" }, result);
    }

    [Fact]
    public void Convert_CollectionBytes_ReturnsByteArrayArray()
    {
        var col = new CollectionBytes();
        col.Bytes.Add(ByteString.CopyFrom(new byte[] { 1 }));
        col.Bytes.Add(ByteString.CopyFrom(new byte[] { 2, 3 }));
        var data = new TypedData { CollectionBytes = col };
        var result = TypedDataConverter.Convert(data);
        var arr = Assert.IsType<byte[][]>(result);
        Assert.Equal(2, arr.Length);
        Assert.Equal(new byte[] { 1 }, arr[0]);
        Assert.Equal(new byte[] { 2, 3 }, arr[1]);
    }

    [Fact]
    public void Convert_CollectionDouble_ReturnsDoubleArray()
    {
        var col = new CollectionDouble();
        col.Double.Add(1.1);
        col.Double.Add(2.2);
        var data = new TypedData { CollectionDouble = col };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal(new[] { 1.1, 2.2 }, result);
    }

    [Fact]
    public void Convert_CollectionSint64_ReturnsLongArray()
    {
        var col = new CollectionSInt64();
        col.Sint64.Add(100L);
        col.Sint64.Add(200L);
        var data = new TypedData { CollectionSint64 = col };
        var result = TypedDataConverter.Convert(data);
        Assert.Equal(new[] { 100L, 200L }, result);
    }

    [Fact]
    public void Convert_ModelBindingData_ReturnsAnonymousObject()
    {
        var mbd = new ModelBindingData
        {
            Version = "1.0",
            Source = "test-source",
            ContentType = "application/json",
            Content = ByteString.CopyFromUtf8("{}")
        };
        var data = new TypedData { ModelBindingData = mbd };
        var result = TypedDataConverter.Convert(data);
        Assert.NotNull(result);
        var type = result!.GetType();
        Assert.Equal("1.0", type.GetProperty("Version")!.GetValue(result));
        Assert.Equal("test-source", type.GetProperty("Source")!.GetValue(result));
        Assert.Equal("application/json", type.GetProperty("ContentType")!.GetValue(result));
    }

    [Fact]
    public void Convert_CollectionModelBindingData_ReturnsObjectArray()
    {
        var mbd1 = new ModelBindingData { Version = "1.0", Source = "s1", ContentType = "text/plain", Content = ByteString.Empty };
        var mbd2 = new ModelBindingData { Version = "2.0", Source = "s2", ContentType = "text/plain", Content = ByteString.Empty };
        var col = new CollectionModelBindingData();
        col.ModelBindingData.Add(mbd1);
        col.ModelBindingData.Add(mbd2);
        var data = new TypedData { CollectionModelBindingData = col };
        var result = TypedDataConverter.Convert(data);
        var arr = Assert.IsType<object[]>(result);
        Assert.Equal(2, arr.Length);
    }

    [Fact]
    public void Convert_Http_ReturnsRpcHttp()
    {
        var http = new RpcHttp { Method = "GET", Url = "http://example.com" };
        var data = new TypedData { Http = http };
        var result = TypedDataConverter.Convert(data);
        Assert.IsType<RpcHttp>(result);
        Assert.Equal("GET", ((RpcHttp)result).Method);
    }

    [Fact]
    public void ParseJsonValue_NullInput_ReturnsNull()
    {
        var result = TypedDataConverter.ParseJsonValue(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ParseJsonValue_ValidJson_ReturnsClonedElement()
    {
        var result = TypedDataConverter.ParseJsonValue("[1,2,3]");
        Assert.IsType<JsonElement>(result);
        var element = (JsonElement)result;
        Assert.Equal(JsonValueKind.Array, element.ValueKind);
    }
}
