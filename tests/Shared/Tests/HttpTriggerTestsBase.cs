using Xunit;

namespace TestProject;

/// <summary>Tests for HttpRequestData CRUD endpoints at /api/items.</summary>
public abstract class HttpTriggerTestsBase : TestHostTestBase
{
    protected HttpTriggerTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task GetItems_ReturnsEmptyList_WhenNoItemsExist()
    {
        var response = await Client.GetAsync("/api/items", TestCancellation);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<Item>>(TestCancellation);
        Assert.NotNull(items);
        Assert.Empty(items);
    }

    [Fact]
    public async Task CreateItem_ReturnsCreated_WithGeneratedId()
    {
        var response = await Client.PostAsJsonAsync("/api/items", new { Name = "Test Item" }, TestCancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.NotNull(item);
        Assert.NotEmpty(item.Id);
        Assert.Equal("Test Item", item.Name);
    }

    [Fact]
    public async Task GetItem_ReturnsItem_WhenExists()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/items", new { Name = "Find Me" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.GetAsync($"/api/items/{created!.Id}", TestCancellation);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal(created.Id, item!.Id);
    }

    [Fact]
    public async Task GetItem_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync($"/api/items/{Guid.NewGuid()}", TestCancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItem_UpdatesExistingItem()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/items", new { Name = "Original" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.PutAsJsonAsync(
            $"/api/items/{created!.Id}",
            new { Name = "Updated", IsCompleted = true },
            TestCancellation);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal("Updated", updated!.Name);
        Assert.True(updated.IsCompleted);
    }

    [Fact]
    public async Task DeleteItem_ReturnsNoContent_WhenExists()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/items", new { Name = "Delete Me" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.DeleteAsync($"/api/items/{created!.Id}", TestCancellation);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.DeleteAsync($"/api/items/{Guid.NewGuid()}", TestCancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetItemAlt_ReturnsItem_WhenHttpTriggerParamNameIsNotReq()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/items", new { Name = "Alt Binding" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.GetAsync($"/api/items/{created!.Id}/alt", TestCancellation);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal(created.Id, item!.Id);
    }

    [Fact]
    public async Task GetItemByBindingData_ReturnsItem_WhenRouteParamInBindingData()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/items", new { Name = "BindingData Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.GetAsync($"/api/items/{created!.Id}/binding-data", TestCancellation);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal(created.Id, item!.Id);
    }

    [Fact]
    public async Task GetItemWithContext_ReturnsItem_WhenFunctionContextInjected()
    {
        var createResponse = await Client.PostAsJsonAsync("/api/items", new { Name = "Context Test" }, TestCancellation);
        var created = await createResponse.Content.ReadFromJsonAsync<Item>(TestCancellation);

        var response = await Client.GetAsync($"/api/items/{created!.Id}/with-context", TestCancellation);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<Item>(TestCancellation);
        Assert.Equal(created.Id, item!.Id);
    }
}
