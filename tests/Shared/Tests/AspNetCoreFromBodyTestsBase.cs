using Xunit;

namespace TestProject;

/// <summary>
/// Tests verifying that the <c>[FromBody]</c> attribute works in ASP.NET Core integration mode
/// with <see cref="Microsoft.AspNetCore.Http.HttpRequest"/>.
/// </summary>
public abstract class AspNetCoreFromBodyTestsBase : TestHostTestBase
{
    protected AspNetCoreFromBodyTestsBase(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task EchoFromBodyAspNetCore_DeserializesBodyViaFromBodyAttribute()
    {
        var response = await Client.PostAsJsonAsync("/api/aspnetcore/echo-from-body", new { Name = "ASP.NET Core FromBody" }, TestCancellation);

        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<CreateItemRequest>(TestCancellation);
        Assert.NotNull(item);
        Assert.Equal("ASP.NET Core FromBody", item.Name);
    }
}
