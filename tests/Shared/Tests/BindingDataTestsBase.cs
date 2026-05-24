using Xunit;

namespace TestProject;

/// <summary>
/// Tests verifying that <c>FunctionContext.BindingContext.BindingData</c> is populated from
/// the HTTP request body, headers, and query string — matching real Azure Functions host behavior.
/// </summary>
public abstract class BindingDataTestsBase : TestHostTestBase
{
    protected BindingDataTestsBase(ITestOutputHelper output) : base(output) { }

    private sealed record BindingDataPayload(
        string StringField,
        int NumberField,
        NestedObject ObjectField,
        string[] ArrayField);

    private sealed record NestedObject(string Inner);

    [Fact]
    public async Task EchoBindingData_StringProperty_PresentInBindingData()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/echo-binding-data",
            new BindingDataPayload("hello", 42, new NestedObject("x"), ["a", "b"]),
            TestCancellation);

        response.EnsureSuccessStatusCode();
        var bindingData = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestCancellation);

        Assert.NotNull(bindingData);
        Assert.True(bindingData.ContainsKey("stringField"), "BindingData should contain 'stringField'");
        Assert.Equal("hello", bindingData["stringField"]);
    }

    [Fact]
    public async Task EchoBindingData_NumberProperty_PresentInBindingDataAsString()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/echo-binding-data",
            new BindingDataPayload("hello", 42, new NestedObject("x"), ["a", "b"]),
            TestCancellation);

        response.EnsureSuccessStatusCode();
        var bindingData = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestCancellation);

        Assert.NotNull(bindingData);
        Assert.True(bindingData.ContainsKey("numberField"), "BindingData should contain 'numberField'");
        Assert.Equal("42", bindingData["numberField"]);
    }

    [Fact]
    public async Task EchoBindingData_ObjectProperty_PresentInBindingDataAsJsonString()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/echo-binding-data",
            new BindingDataPayload("hello", 42, new NestedObject("x"), ["a", "b"]),
            TestCancellation);

        response.EnsureSuccessStatusCode();
        var bindingData = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestCancellation);

        Assert.NotNull(bindingData);
        Assert.True(bindingData.ContainsKey("objectField"), "BindingData should contain 'objectField'");
        // Value should be a JSON string representing the nested object.
        var objectJson = bindingData["objectField"];
        Assert.NotNull(objectJson);
        Assert.Contains("inner", objectJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EchoBindingData_ArrayProperty_AbsentFromBindingData()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/echo-binding-data",
            new BindingDataPayload("hello", 42, new NestedObject("x"), ["a", "b"]),
            TestCancellation);

        response.EnsureSuccessStatusCode();
        var bindingData = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestCancellation);

        Assert.NotNull(bindingData);
        // Arrays are excluded by the real Azure Functions host — the framework should match this.
        Assert.False(bindingData.ContainsKey("arrayField"), "BindingData should NOT contain 'arrayField'");
    }

    [Fact]
    public async Task EchoBindingData_QueryAndHeaders_PresentInBindingData()
    {
        var url = "/api/echo-binding-data?foo=bar";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { Value = "test" })
        };

        var response = await Client.SendAsync(request, TestCancellation);
        response.EnsureSuccessStatusCode();

        var bindingData = await response.Content
            .ReadFromJsonAsync<Dictionary<string, string?>>(TestCancellation);

        Assert.NotNull(bindingData);
        Assert.True(bindingData.ContainsKey("Query"), "BindingData should contain 'Query'");
        Assert.True(bindingData.ContainsKey("Headers"), "BindingData should contain 'Headers'");

        // Query should contain the foo=bar parameter.
        var queryJson = bindingData["Query"];
        Assert.NotNull(queryJson);
        Assert.Contains("foo", queryJson, StringComparison.OrdinalIgnoreCase);
    }
}
