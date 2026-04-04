using Xunit;

namespace TestProject;

/// <summary>Tests for HttpRequest CRUD endpoints at /api/aspnetcore/items. ASP.NET Core flavors only.</summary>
public abstract class AspNetCoreHttpTriggerTestsBase : TestHostTestBase
{
    protected AspNetCoreHttpTriggerTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task GetItemsAspNetCore_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/api/aspnetcore/items", TestCancellation);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<Item>>(TestCancellation);
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task CreateItemAspNetCore_ReturnsCreated()
    {
        var response = await Client.PostAsJsonAsync("/api/aspnetcore/items", new { Name = "ANC Item" }, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.NotNull(item);
        Assert.NotEmpty(item.Id);
    }

    [Fact]
    public async Task GetItemAspNetCore_ReturnsItem_WhenExists()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/aspnetcore/items", new { Name = "Find ANC" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.GetAsync($"/api/aspnetcore/items/{created!.Id}", TestCancellation);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal(created.Id, item!.Id);
    }

    [Fact]
    public async Task GetItemByGuidAspNetCore_ReturnsItem_WhenExistsAsGuid()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/aspnetcore/items", new { Name = "Guid Item" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.GetAsync($"/api/aspnetcore/items/by-guid/{created!.Id}", TestCancellation);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal(created.Id, item!.Id);
    }

    [Fact]
    public async Task UpdateItemAspNetCore_UpdatesExistingItem()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/aspnetcore/items", new { Name = "Original ANC" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.PutAsJsonAsync(
            $"/api/aspnetcore/items/{created!.Id}",
            new { Name = "Updated ANC", IsCompleted = true },
            TestCancellation);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal("Updated ANC", updated!.Name);
    }

    [Fact]
    public async Task DeleteItemAspNetCore_ReturnsNoContent_WhenExists()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/aspnetcore/items", new { Name = "Delete ANC" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.DeleteAsync($"/api/aspnetcore/items/{created!.Id}", TestCancellation);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Verifies that the correlation middleware can read the request header via
    /// <c>context.GetHttpRequestDataAsync()</c> in ASP.NET Core / Kestrel mode.
    /// (In direct gRPC mode the HTTP bindings are not yet resolved when middleware runs,
    /// so this test is ASP.NET Core-only.)
    /// </summary>
    [Fact]
    public async Task CorrelationEndpoint_ReturnsHeaderValue_FromMiddleware_AspNetCore()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/correlation");
        request.Headers.Add(CorrelationMiddleware.HeaderName, "aspnetcore-correlation-id");

        var response = await Client.SendAsync(request, TestCancellation);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<CorrelationIdResponse>(TestCancellation);
        Assert.NotNull(payload);
        Assert.Equal("aspnetcore-correlation-id", payload.CorrelationId);
    }
}
