using AzureFunctions.TestFramework.Core;
using Xunit.Abstractions;

namespace TestProject;

/// <summary>Tests verifying custom route prefix (v1) routing with HttpRequestData.</summary>
public abstract class CustomRoutePrefixTestsBase : TestHostTestBase
{
    protected CustomRoutePrefixTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task GetItems_WithCustomRoutePrefix_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/v1/items", TestCancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_WithDefaultApiPrefix_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/items", TestCancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateItem_WithCustomRoutePrefix_ReturnsCreated()
    {
        var content = new StringContent("{\"name\":\"Widget\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/v1/items", content, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetItem_WithCustomRoutePrefix_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync($"/v1/items/{Guid.NewGuid()}", TestCancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_WithCustomRoutePrefix_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync($"/v1/items/{Guid.NewGuid()}", TestCancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
