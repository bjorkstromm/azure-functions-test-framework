using AzureFunctions.TestFramework.Core;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for the static factory methods on <see cref="FunctionBindingData"/>.
/// </summary>
public class FunctionBindingDataTests
{
    [Fact]
    public void WithBytes_SetsNameAndBytes()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var data = FunctionBindingData.WithBytes("myParam", bytes);

        Assert.Equal("myParam", data.Name);
        Assert.Equal(bytes, data.Bytes);
        Assert.Null(data.Json);
        Assert.Null(data.StringValue);
        Assert.Null(data.ModelBindingData);
        Assert.Null(data.CollectionModelBindingData);
    }

    [Fact]
    public void WithJson_SetsNameAndJson()
    {
        const string json = """{"key":"value"}""";
        var data = FunctionBindingData.WithJson("myParam", json);

        Assert.Equal("myParam", data.Name);
        Assert.Equal(json, data.Json);
        Assert.Null(data.Bytes);
        Assert.Null(data.StringValue);
    }

    [Fact]
    public void WithString_SetsNameAndStringValue()
    {
        var data = FunctionBindingData.WithString("myParam", "hello");

        Assert.Equal("myParam", data.Name);
        Assert.Equal("hello", data.StringValue);
        Assert.Null(data.Bytes);
        Assert.Null(data.Json);
    }

    [Fact]
    public void WithModelBindingData_SetsModelBindingData()
    {
        var mbd = new ModelBindingDataValue
        {
            Source = "source1",
            ContentType = "application/json",
            Content = new byte[] { 42 }
        };
        var data = FunctionBindingData.WithModelBindingData("myParam", mbd);

        Assert.Equal("myParam", data.Name);
        Assert.Same(mbd, data.ModelBindingData);
        Assert.Null(data.CollectionModelBindingData);
    }

    [Fact]
    public void WithCollectionModelBindingData_SetsCollection()
    {
        var items = new List<ModelBindingDataValue>
        {
            new() { Source = "s1", ContentType = "text/plain", Content = Array.Empty<byte>() },
            new() { Source = "s2", ContentType = "text/plain", Content = Array.Empty<byte>() }
        };
        var data = FunctionBindingData.WithCollectionModelBindingData("myParam", items);

        Assert.Equal("myParam", data.Name);
        Assert.Equal(2, data.CollectionModelBindingData!.Count);
        Assert.Null(data.ModelBindingData);
    }
}
