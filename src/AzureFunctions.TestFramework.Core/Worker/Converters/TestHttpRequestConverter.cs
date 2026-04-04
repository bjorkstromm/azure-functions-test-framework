using Microsoft.Azure.Functions.Worker.Converters;

namespace AzureFunctions.TestFramework.Core.Worker.Converters;

/// <summary>
/// Workaround input converter for <c>HttpRequest</c> parameters in ASP.NET Core integration mode
/// (<c>ConfigureFunctionsWebApplication</c>).
///
/// In the normal Azure Functions host the worker runs as a separate process, so all
/// ASP.NET Core types share a single assembly identity. When the worker is hosted
/// in-process (as this test framework does), the test runner and the worker may resolve
/// <c>Microsoft.AspNetCore.Http.Abstractions</c> types from different copies of the assembly,
/// causing the <c>requestContext is HttpContext</c> type-identity check in
/// <c>FunctionContextExtensions.TryGetRequest</c> (inside the production
/// <c>HttpContextConverter</c>) to return <c>false</c> at runtime even though the object
/// is clearly a <c>DefaultHttpContext</c> in the debugger.
///
/// This converter bypasses the problematic <c>is</c> check by:
/// 1. Using <c>FullName</c> string comparison instead of a runtime type-identity check.
/// 2. Accessing the <c>Request</c> property via reflection, which avoids any cast through
///    the potentially mismatched <c>HttpContext</c> reference.
///
/// The returned <c>HttpRequest</c> object is from Kestrel's own <c>DefaultHttpContext</c>
/// and therefore has the correct assembly identity for the cast in the generated
/// <c>DirectFunctionExecutor</c>.
/// </summary>
internal sealed class TestHttpRequestConverter : IInputConverter
{
    private const string HttpContextItemsKey = "HttpRequestContext";
    private const string HttpRequestFullName = "Microsoft.AspNetCore.Http.HttpRequest";

    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        // DIAGNOSTIC: Log to stderr so we can see if this converter is reached
        System.Console.Error.WriteLine($"[TestHttpRequestConverter] ConvertAsync called: TargetType={context.TargetType.FullName}, Source={context.Source?.GetType().FullName ?? "null"}");

        if (context.TargetType.FullName != HttpRequestFullName)
        {
            return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
        }

        System.Console.Error.WriteLine($"[TestHttpRequestConverter] TargetType matches HttpRequest");

        if (!context.FunctionContext.Items.TryGetValue(HttpContextItemsKey, out var httpContextObj)
            || httpContextObj is null)
        {
            System.Console.Error.WriteLine($"[TestHttpRequestConverter] HttpRequestContext NOT found in Items. Keys: {string.Join(", ", context.FunctionContext.Items.Keys)}");
            return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
        }

        System.Console.Error.WriteLine($"[TestHttpRequestConverter] HttpRequestContext found: {httpContextObj.GetType().FullName}");

        // Use reflection to get the Request property, bypassing any 'is HttpContext'
        // type-identity check that could fail when assemblies are loaded in-process.
        var requestProp = httpContextObj.GetType().GetProperty("Request");
        var request = requestProp?.GetValue(httpContextObj);

        System.Console.Error.WriteLine($"[TestHttpRequestConverter] Request: {request?.GetType().FullName ?? "null"}");

        if (request is not null)
        {
            return new ValueTask<ConversionResult>(ConversionResult.Success(request));
        }

        return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
    }
}
