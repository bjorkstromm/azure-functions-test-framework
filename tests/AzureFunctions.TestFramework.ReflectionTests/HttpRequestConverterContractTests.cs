using System.Reflection;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// SDK contract tests for <c>TestHttpRequestConverter</c>.
///
/// <para>Verifies that <c>HttpContext.Request</c> is accessible via reflection using the
/// property name <c>"Request"</c>, as <c>TestHttpRequestConverter</c> does to avoid
/// ALC type-identity checks.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 4 for full context.</para>
/// </summary>
public class HttpRequestConverterContractTests
{
    private const string HttpRequestFullName = "Microsoft.AspNetCore.Http.HttpRequest";
    private const string HttpContextItemsKey = "HttpRequestContext";

    [Fact]
    public void HttpContext_HasRequestProperty()
    {
        var requestProp = typeof(HttpContext).GetProperty("Request");
        Assert.NotNull(requestProp);
    }

    [Fact]
    public void HttpContext_Request_IsHttpRequest()
    {
        var requestProp = typeof(HttpContext).GetProperty("Request");
        Assert.NotNull(requestProp);
        Assert.Equal(typeof(HttpRequest), requestProp.PropertyType);
    }

    [Fact]
    public void HttpRequest_FullNameMatchesExpectedConstant()
    {
        // TestHttpRequestConverter compares ConverterContext.TargetType.FullName against
        // this constant. If ASP.NET Core ever moves HttpRequest to a new namespace, this test fails.
        Assert.Equal(HttpRequestFullName, typeof(HttpRequest).FullName);
    }

    [Fact]
    public void HttpContext_Request_CanBeReadViaReflection()
    {
        // Simulate what TestHttpRequestConverter does: retrieve Request via GetProperty("Request").
        // We use DefaultHttpContext (a concrete implementation) to verify the property is readable.
        var context = new DefaultHttpContext();
        var requestProp = context.GetType().GetProperty("Request");
        Assert.NotNull(requestProp);

        var request = requestProp.GetValue(context);
        Assert.NotNull(request);
        Assert.IsAssignableFrom<HttpRequest>(request);
    }
}
