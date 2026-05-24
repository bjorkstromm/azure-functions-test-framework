using Xunit;

namespace TestProject;

/// <summary>Tests for custom route prefix + HttpRequest (ASP.NET Core flavors only).</summary>
public abstract class AspNetCoreCustomRoutePrefixTestsBase : TestHostTestBase
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    protected AspNetCoreCustomRoutePrefixTestsBase(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task GetItemsAspNetCore_WithCustomRoutePrefix_ReturnsOk()
    {
        var response = await Client.GetAsync("/v1/aspnetcore/items", TestCancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task CreateItemAspNetCore_WithCustomRoutePrefix_ReturnsCreated()
    {
        var content = new StringContent("{\"name\":\"ANC Widget\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/v1/aspnetcore/items", content, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
