using Microsoft.Azure.Functions.Worker.Converters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Core.Worker.Converters;

/// <summary>
/// Workaround input converter for <c>HttpRequest</c> parameters in ASP.NET Core integration mode
/// (<c>ConfigureFunctionsWebApplication</c>).
///
/// <para><b>Root cause</b>: when the worker middleware runs inside the test framework's
/// in-process host, the SDK's <c>IBindingCache</c> may be populated before
/// <c>FunctionsHttpProxyingMiddleware</c> has added the <c>HttpContext</c> to
/// <c>FunctionContext.Items</c>.  As a result the production
/// <c>HttpContextConverter</c> cannot resolve the <c>HttpRequest</c> from Items and the
/// generated <c>DirectFunctionExecutor</c> falls back to the raw
/// <c>GrpcHttpRequestData</c>, which cannot be cast to <c>HttpRequest</c>.</para>
///
/// <para>This converter is registered at position 0 in the converter pipeline.
/// When <c>Items["HttpRequestContext"]</c> contains a valid <c>HttpContext</c>, it
/// extracts the <c>Request</c> property via reflection (avoiding any <c>is</c>-check
/// that could theoretically fail under assembly-load-context mismatch) and returns it.</para>
/// </summary>
internal sealed class TestHttpRequestConverter : IInputConverter
{
    private const string HttpContextItemsKey = "HttpRequestContext";
    private const string HttpRequestFullName = "Microsoft.AspNetCore.Http.HttpRequest";

    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        var logger = context.FunctionContext.InstanceServices.GetService<ILogger<TestHttpRequestConverter>>();
        logger?.LogWarning(
            "CONV TestHttpRequestConverter: invId={InvId}, func={Func}, TargetType={TT}, itemCount={IC}, hasHttpCtx={HHC}",
            context.FunctionContext.InvocationId,
            context.FunctionContext.FunctionDefinition?.Name,
            context.TargetType.FullName,
            context.FunctionContext.Items.Count,
            context.FunctionContext.Items.ContainsKey(HttpContextItemsKey));

        // Stack trace to understand caller chain
        var stackFrames = new System.Diagnostics.StackTrace(skipFrames: 0, fNeedFileInfo: false)
            .GetFrames()
            .Select(f => $"{f.GetMethod()?.DeclaringType?.Name}.{f.GetMethod()?.Name}")
            .Take(30)
            .ToArray();
        logger?.LogWarning("CONV STACK: {Frames}", string.Join(" → ", stackFrames));

        if (context.TargetType.FullName != HttpRequestFullName)
        {
            return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
        }

        if (!context.FunctionContext.Items.TryGetValue(HttpContextItemsKey, out var httpContextObj)
            || httpContextObj is null)
        {
            logger?.LogWarning("CONV TestHttpRequestConverter: returning Unhandled (no HttpRequestContext in Items)");
            return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
        }

        // Use reflection to get the Request property, bypassing any 'is HttpContext'
        // type-identity check that could fail when assemblies are loaded in-process.
        var requestProp = httpContextObj.GetType().GetProperty("Request");
        var request = requestProp?.GetValue(httpContextObj);

        if (request is not null)
        {
            logger?.LogWarning("CONV TestHttpRequestConverter: returning Success({Type})", request.GetType().FullName);
            return new ValueTask<ConversionResult>(ConversionResult.Success(request));
        }

        logger?.LogWarning("CONV TestHttpRequestConverter: returning Unhandled (Request property was null)");
        return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
    }
}
