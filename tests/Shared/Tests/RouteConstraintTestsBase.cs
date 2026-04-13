using Xunit;

namespace TestProject;

/// <summary>
/// Shared test base for route constraint, optional parameter, and catch-all route coverage.
/// Exercises the <see cref="AzureFunctions.TestFramework.Core.Routing.RouteMatcher"/> in
/// gRPC-direct mode and verifies that ASP.NET Core integration mode handles the same
/// scenarios through its own routing engine.
/// </summary>
public abstract class RouteConstraintTestsBase : TestHostTestBase
{
    protected RouteConstraintTestsBase(ITestOutputHelper output) : base(output) { }

    // ── Int constraint ────────────────────────────────────────────────────────

    [Fact]
    public async Task IntConstraint_NumericValue_MatchesIntRoute()
    {
        var response = await Client.GetAsync("/api/items-constrained/42", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteEchoResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Equal("int", body.Type);
        Assert.Equal("42", body.Value);
    }

    [Fact]
    public async Task IntConstraint_NegativeInteger_MatchesIntRoute()
    {
        // Negative integers are valid int values.
        var response = await Client.GetAsync("/api/items-constrained/-5", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteEchoResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Equal("int", body.Type);
    }

    // ── Alpha constraint ──────────────────────────────────────────────────────

    [Fact]
    public async Task AlphaConstraint_AlphaValue_MatchesAlphaRoute()
    {
        var response = await Client.GetAsync("/api/items-constrained/hello", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteEchoResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Equal("alpha", body.Type);
        Assert.Equal("hello", body.Value);
    }

    // ── Best-match ordering ───────────────────────────────────────────────────

    [Fact]
    public async Task BestMatch_NumericSegment_RoutesToIntNotAlpha()
    {
        // "99" satisfies {id:int} but NOT {name:alpha} — should always hit the int route.
        var response = await Client.GetAsync("/api/items-constrained/99", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteEchoResponse>(TestCancellation);
        Assert.Equal("int", body!.Type);
    }

    [Fact]
    public async Task BestMatch_AlphaSegment_RoutesToAlphaNotInt()
    {
        // "world" satisfies {name:alpha} but NOT {id:int} — should always hit the alpha route.
        var response = await Client.GetAsync("/api/items-constrained/world", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteEchoResponse>(TestCancellation);
        Assert.Equal("alpha", body!.Type);
    }

    [Fact]
    public async Task NoMatchingConstraint_Returns404()
    {
        // "42abc" is neither a valid int nor all-letters — no route matches.
        var response = await Client.GetAsync("/api/items-constrained/42abc", TestCancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Optional parameter ────────────────────────────────────────────────────

    [Fact]
    public async Task OptionalParam_WithValue_ReturnsValue()
    {
        var response = await Client.GetAsync("/api/items-optional/hello", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OptionalPageResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Equal("hello", body.Page);
    }

    [Fact]
    public async Task OptionalParam_Absent_ReturnsNullPage()
    {
        var response = await Client.GetAsync("/api/items-optional", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OptionalPageResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Null(body.Page);
    }

    // ── Catch-all parameter ───────────────────────────────────────────────────

    [Fact]
    public async Task CatchAll_SingleSegment_CapturesSegment()
    {
        var response = await Client.GetAsync("/api/files/readme.txt", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CatchAllResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Equal("readme.txt", body.Rest);
    }

    [Fact]
    public async Task CatchAll_MultipleSegments_CapturesAll()
    {
        var response = await Client.GetAsync("/api/files/a/b/c.txt", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CatchAllResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Equal("a/b/c.txt", body.Rest);
    }

    // ── Combined constraints ──────────────────────────────────────────────────

    [Fact]
    public async Task RangeConstraint_ValueInRange_Matches()
    {
        var response = await Client.GetAsync("/api/items-range/50", TestCancellation);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RouteEchoResponse>(TestCancellation);
        Assert.NotNull(body);
        Assert.Equal("ranged-int", body.Type);
        Assert.Equal("50", body.Value);
    }

    [Fact]
    public async Task RangeConstraint_ValueBelowMin_Returns404()
    {
        // min(1) means 0 should not match.
        var response = await Client.GetAsync("/api/items-range/0", TestCancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RangeConstraint_ValueAboveMax_Returns404()
    {
        // max(100) means 101 should not match.
        var response = await Client.GetAsync("/api/items-range/101", TestCancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
