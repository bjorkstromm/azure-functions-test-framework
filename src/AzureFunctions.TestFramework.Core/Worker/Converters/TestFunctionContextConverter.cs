using Microsoft.Azure.Functions.Worker.Converters;

namespace AzureFunctions.TestFramework.Core.Worker.Converters;

/// <summary>
/// Workaround input converter for <c>FunctionContext</c> parameters in ASP.NET Core integration mode
/// (<c>ConfigureFunctionsWebApplication</c>).
///
/// In-process hosting can cause the same assembly to be loaded twice into
/// <c>AssemblyLoadContext.Default</c>: once by the test runner and once via
/// <c>DefaultMethodInfoLocator.GetMethod</c>'s <c>LoadFromAssemblyPath</c> call, which adds
/// the function app directory to a "load-from" probe list.  When that happens,
/// <c>context.TargetType == typeof(FunctionContext)</c> (used by <c>FunctionContextConverter</c>)
/// returns <c>false</c> at runtime even though both sides look identical in the debugger —
/// classic assembly-identity mismatch.
///
/// This converter bypasses the failing <c>==</c> check by comparing <c>FullName</c> strings,
/// which is immune to dual-load scenarios.
/// </summary>
internal sealed class TestFunctionContextConverter : IInputConverter
{
    private const string FunctionContextFullName = "Microsoft.Azure.Functions.Worker.FunctionContext";

    public ValueTask<ConversionResult> ConvertAsync(ConverterContext context)
    {
        if (context.TargetType.FullName != FunctionContextFullName)
        {
            return new ValueTask<ConversionResult>(ConversionResult.Unhandled());
        }

        return new ValueTask<ConversionResult>(ConversionResult.Success(context.FunctionContext));
    }
}
