using AzureFunctions.TestFramework.Http;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Http;

/// <summary>
/// Unit tests for <see cref="HttpRequestMapper.ExtractRouteParameters"/>.
/// </summary>
public class HttpRequestMapperTests
{
    private readonly HttpRequestMapper _mapper = new();

    [Fact]
    public void ExtractRouteParameters_SimpleParam_ReturnsParam()
    {
        var result = _mapper.ExtractRouteParameters("api/todos/{id}", "api/todos/123");
        Assert.Single(result);
        Assert.Equal("123", result["id"]);
    }

    [Fact]
    public void ExtractRouteParameters_MultipleParams_ReturnsAll()
    {
        var result = _mapper.ExtractRouteParameters(
            "api/{version}/users/{userId}/orders/{orderId}",
            "api/v2/users/42/orders/99");
        Assert.Equal(3, result.Count);
        Assert.Equal("v2", result["version"]);
        Assert.Equal("42", result["userId"]);
        Assert.Equal("99", result["orderId"]);
    }

    [Fact]
    public void ExtractRouteParameters_NoParams_ReturnsEmpty()
    {
        var result = _mapper.ExtractRouteParameters("api/todos", "api/todos");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractRouteParameters_TemplateLongerThanPath_OnlyMatchesAvailable()
    {
        var result = _mapper.ExtractRouteParameters(
            "api/{a}/{b}/{c}",
            "api/x/y");
        Assert.Equal(2, result.Count);
        Assert.Equal("x", result["a"]);
        Assert.Equal("y", result["b"]);
    }

    [Fact]
    public void ExtractRouteParameters_PathLongerThanTemplate_IgnoresExtra()
    {
        var result = _mapper.ExtractRouteParameters(
            "api/{id}",
            "api/42/extra/segments");
        Assert.Single(result);
        Assert.Equal("42", result["id"]);
    }

    [Fact]
    public void ExtractRouteParameters_EmptyTemplate_ReturnsEmpty()
    {
        var result = _mapper.ExtractRouteParameters("", "api/todos/1");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractRouteParameters_EmptyPath_ReturnsEmpty()
    {
        var result = _mapper.ExtractRouteParameters("api/todos/{id}", "");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractRouteParameters_StaticSegmentNotCaptured()
    {
        var result = _mapper.ExtractRouteParameters("api/{id}/details", "api/99/details");
        Assert.Single(result);
        Assert.Equal("99", result["id"]);
        Assert.False(result.ContainsKey("details"));
    }
}
