using AzureFunctions.TestFramework.Core.Routing;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="RouteMatcher"/>.
/// </summary>
public class RouteMatcherTests
{
    private readonly RouteMatcher _matcher = new();

    // ── Literal routes ────────────────────────────────────────────────────────

    [Fact]
    public void Match_LiteralRoute_ExactMatch_ReturnsId()
    {
        _matcher.AddRoute("GET", "todos", "fn1");

        var (id, _) = _matcher.Match("GET", "todos");

        Assert.Equal("fn1", id);
    }

    [Fact]
    public void Match_LiteralRoute_DifferentMethod_ReturnsNull()
    {
        _matcher.AddRoute("GET", "todos", "fn1");

        var (id, _) = _matcher.Match("POST", "todos");

        Assert.Null(id);
    }

    [Fact]
    public void Match_LiteralRoute_NoMatch_ReturnsNull()
    {
        _matcher.AddRoute("GET", "todos", "fn1");

        var (id, _) = _matcher.Match("GET", "items");

        Assert.Null(id);
    }

    // ── Parameterised routes ──────────────────────────────────────────────────

    [Fact]
    public void Match_ParameterisedRoute_ExtractsParam()
    {
        _matcher.AddRoute("GET", "todos/{id}", "fn1");

        var (id, routeParams) = _matcher.Match("GET", "todos/42");

        Assert.Equal("fn1", id);
        Assert.Equal("42", routeParams["id"]);
    }

    [Fact]
    public void Match_MultipleParams_ExtractsAll()
    {
        _matcher.AddRoute("GET", "users/{userId}/orders/{orderId}", "fn1");

        var (id, routeParams) = _matcher.Match("GET", "users/10/orders/99");

        Assert.Equal("fn1", id);
        Assert.Equal("10", routeParams["userId"]);
        Assert.Equal("99", routeParams["orderId"]);
    }

    // ── Route constraints ─────────────────────────────────────────────────────

    [Fact]
    public void Match_IntConstraint_ValidInt_Matches()
    {
        _matcher.AddRoute("GET", "todos/{id:int}", "fn1");

        var (id, routeParams) = _matcher.Match("GET", "todos/42");

        Assert.Equal("fn1", id);
        Assert.Equal("42", routeParams["id"]);
    }

    [Fact]
    public void Match_IntConstraint_NonInt_ReturnsNull()
    {
        _matcher.AddRoute("GET", "todos/{id:int}", "fn1");

        var (id, _) = _matcher.Match("GET", "todos/abc");

        Assert.Null(id);
    }

    [Fact]
    public void Match_GuidConstraint_ValidGuid_Matches()
    {
        _matcher.AddRoute("GET", "items/{id:guid}", "fn1");
        var guid = Guid.NewGuid().ToString();

        var (id, routeParams) = _matcher.Match("GET", $"items/{guid}");

        Assert.Equal("fn1", id);
        Assert.Equal(guid, routeParams["id"]);
    }

    [Fact]
    public void Match_GuidConstraint_NotAGuid_ReturnsNull()
    {
        _matcher.AddRoute("GET", "items/{id:guid}", "fn1");

        var (id, _) = _matcher.Match("GET", "items/not-a-guid");

        Assert.Null(id);
    }

    [Fact]
    public void Match_AlphaConstraint_AlphaOnly_Matches()
    {
        _matcher.AddRoute("GET", "cats/{name:alpha}", "fn1");

        var (id, routeParams) = _matcher.Match("GET", "cats/fluffy");

        Assert.Equal("fn1", id);
        Assert.Equal("fluffy", routeParams["name"]);
    }

    [Fact]
    public void Match_AlphaConstraint_HasDigits_ReturnsNull()
    {
        _matcher.AddRoute("GET", "cats/{name:alpha}", "fn1");

        var (id, _) = _matcher.Match("GET", "cats/fluffy123");

        Assert.Null(id);
    }

    // ── Optional parameters ───────────────────────────────────────────────────

    [Fact]
    public void Match_OptionalParam_Present_ExtractsValue()
    {
        _matcher.AddRoute("GET", "items/{page?}", "fn1");

        var (id, routeParams) = _matcher.Match("GET", "items/2");

        Assert.Equal("fn1", id);
        Assert.Equal("2", routeParams["page"]);
    }

    [Fact]
    public void Match_OptionalParam_Absent_MatchesWithoutKey()
    {
        _matcher.AddRoute("GET", "items/{page?}", "fn1");

        var (id, routeParams) = _matcher.Match("GET", "items");

        Assert.Equal("fn1", id);
        Assert.False(routeParams.ContainsKey("page"));
    }

    // ── Catch-all ─────────────────────────────────────────────────────────────

    [Fact]
    public void Match_CatchAll_Matches()
    {
        _matcher.AddRoute("GET", "files/{*rest}", "fn1");

        var (id, routeParams) = _matcher.Match("GET", "files/a/b/c.txt");

        Assert.Equal("fn1", id);
        Assert.True(routeParams.ContainsKey("rest"));
    }

    // ── Route priority ────────────────────────────────────────────────────────

    [Fact]
    public void Match_LiteralBeatsParam_ReturnsLiteralFunctionId()
    {
        _matcher.AddRoute("GET", "todos/special", "literal-fn");
        _matcher.AddRoute("GET", "todos/{id}", "param-fn");

        var (id, _) = _matcher.Match("GET", "todos/special");

        Assert.Equal("literal-fn", id);
    }

    [Fact]
    public void Match_ConstrainedParamBeatsUnconstrained_ReturnsConstrainedId()
    {
        _matcher.AddRoute("GET", "todos/{id}", "unconstrained-fn");
        _matcher.AddRoute("GET", "todos/{id:int}", "constrained-fn");

        var (id, _) = _matcher.Match("GET", "todos/42");

        Assert.Equal("constrained-fn", id);
    }

    // ── Case-insensitive method ───────────────────────────────────────────────

    [Fact]
    public void Match_MethodCaseInsensitive_Matches()
    {
        _matcher.AddRoute("GET", "todos", "fn1");

        var (id, _) = _matcher.Match("get", "todos");

        Assert.Equal("fn1", id);
    }

    // ── Empty routes ──────────────────────────────────────────────────────────

    [Fact]
    public void Match_NoRoutesRegistered_ReturnsNull()
    {
        var (id, routeParams) = _matcher.Match("GET", "todos");

        Assert.Null(id);
        Assert.Empty(routeParams);
    }

    // ── Leading slash in path ─────────────────────────────────────────────────

    [Fact]
    public void Match_PathWithLeadingSlash_Matches()
    {
        _matcher.AddRoute("GET", "todos", "fn1");

        var (id, _) = _matcher.Match("GET", "/todos");

        Assert.Equal("fn1", id);
    }
}
