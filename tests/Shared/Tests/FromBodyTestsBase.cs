using Xunit;

namespace TestProject;

/// <summary>
/// Tests verifying that the <c>[FromBody]</c> attribute correctly deserializes
/// the HTTP request body into a typed parameter.
/// </summary>
public abstract class FromBodyTestsBase : TestHostTestBase
{
    protected FromBodyTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task EchoFromBody_DeserializesBodyViaFromBodyAttribute()
    {
        var response = await Client.PostAsJsonAsync("/api/echo-from-body", new { Name = "FromBody Test" }, TestCancellation);

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<CreateItemRequest>(TestCancellation);
        Assert.NotNull(item);
        Assert.Equal("FromBody Test", item.Name);
    }
}
