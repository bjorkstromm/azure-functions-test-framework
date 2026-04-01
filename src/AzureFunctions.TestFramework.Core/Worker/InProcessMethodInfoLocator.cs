using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureFunctions.TestFramework.Core.Worker;

/// <summary>
/// Replaces the Worker SDK's internal <c>DefaultMethodInfoLocator</c> to prevent
/// <c>AssemblyLoadContext.Default.LoadFromAssemblyPath</c> from being called during
/// <c>FunctionLoadRequest</c> processing.
///
/// <para>
/// When the test framework hosts the worker in-process, the SDK's default locator
/// loads the function assembly via <c>LoadFromAssemblyPath</c>, which can introduce a
/// second copy of the same assembly into the Default ALC — classic type-identity mismatch.
/// This replacement searches <see cref="AppDomain.CurrentDomain"/> for an already-loaded
/// assembly first, eliminating the dual-load problem at its root.
/// </para>
///
/// <para>
/// Because <c>IMethodInfoLocator</c> is internal to the Worker SDK, registration uses
/// <see cref="DispatchProxy"/> via reflection (see <see cref="TryRegister"/>).
/// </para>
/// </summary>
public static class InProcessMethodInfoLocator
{
    private const string InterfaceFullName =
        "Microsoft.Azure.Functions.Worker.Invocation.IMethodInfoLocator";

    /// <summary>
    /// Registers an in-process <c>IMethodInfoLocator</c> that avoids
    /// <c>LoadFromAssemblyPath</c>.  Must be called <b>before</b>
    /// <c>ConfigureFunctionsWorkerDefaults</c> so the SDK's
    /// <c>TryAddSingleton</c> skips the default implementation.
    /// </summary>
    /// <returns><see langword="true"/> if registration succeeded.</returns>
    public static bool TryRegister(IServiceCollection services, ILogger logger)
    {
        try
        {
            var workerCoreAssembly = typeof(WorkerOptions).Assembly;
            var locatorInterface = workerCoreAssembly.GetType(InterfaceFullName);

            if (locatorInterface is null)
            {
                logger.LogWarning(
                    "Could not find {Interface} in {Assembly} — falling back to default locator",
                    InterfaceFullName, workerCoreAssembly.GetName().Name);
                return false;
            }

            // Create a DispatchProxy that implements the internal interface.
            var createMethod = typeof(DispatchProxy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Create" && m.GetGenericArguments().Length == 2)
                .MakeGenericMethod(locatorInterface, typeof(MethodInfoLocatorProxy));

            var proxy = (MethodInfoLocatorProxy)createMethod.Invoke(null, null)!;
            proxy.Initialize(logger);

            // AddSingleton (not TryAdd) so the SDK's TryAddSingleton is a no-op.
            services.AddSingleton(locatorInterface, proxy);

            logger.LogDebug("Registered in-process IMethodInfoLocator (LoadFromAssemblyPath bypassed)");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to register in-process IMethodInfoLocator — falling back to default locator");
            return false;
        }
    }
}

/// <summary>
/// <see cref="DispatchProxy"/> subclass that implements the Worker SDK's internal
/// <c>IMethodInfoLocator</c> interface.  The proxy intercepts
/// <c>GetMethod(string pathToAssembly, string entryPoint)</c> calls and resolves them
/// from already-loaded assemblies instead of calling <c>LoadFromAssemblyPath</c>.
/// </summary>
public class MethodInfoLocatorProxy : DispatchProxy
{
    private ILogger? _logger;

    /// <summary>
    /// Initializes the proxy with a logger.  Called once after
    /// <see cref="DispatchProxy.Create{T, TProxy}"/> (which uses a parameterless ctor).
    /// </summary>
    internal void Initialize(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "GetMethod" && args?.Length == 2)
        {
            return ResolveMethod((string)args[0]!, (string)args[1]!);
        }

        throw new NotSupportedException($"Unexpected call to {targetMethod?.Name}");
    }

    private MethodInfo ResolveMethod(string pathToAssembly, string entryPoint)
    {
        // Parse entry point: "Namespace.ClassName.MethodName"
        var lastDot = entryPoint.LastIndexOf('.');
        if (lastDot <= 0)
        {
            throw new InvalidOperationException(
                "Invalid entry point configuration. " +
                "The function entry point must be defined in the format <fulltypename>.<methodname>");
        }

        var typeName = entryPoint[..lastDot];
        var methodName = entryPoint[(lastDot + 1)..];

        // First: search already-loaded assemblies by file name.
        var assemblyFileName = Path.GetFileName(pathToAssembly);
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a =>
                !a.IsDynamic &&
                !string.IsNullOrEmpty(a.Location) &&
                string.Equals(
                    Path.GetFileName(a.Location),
                    assemblyFileName,
                    StringComparison.OrdinalIgnoreCase));

        if (assembly is not null)
        {
            _logger?.LogDebug(
                "Resolved {Assembly} from already-loaded assemblies (avoided LoadFromAssemblyPath)",
                assemblyFileName);
        }
        else
        {
            // Fallback: load from path (same behavior as DefaultMethodInfoLocator).
            _logger?.LogWarning(
                "Assembly {Assembly} not found in AppDomain — falling back to LoadFromAssemblyPath",
                assemblyFileName);
            assembly = System.Runtime.Loader.AssemblyLoadContext.Default
                .LoadFromAssemblyPath(pathToAssembly);
        }

        var functionType = assembly.GetType(typeName)
            ?? throw new InvalidOperationException(
                $"Type '{typeName}' not found in assembly '{assembly.GetName().Name}'.");

        var methodInfo = functionType.GetMethod(methodName)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' specified in entry point was not found. " +
                "This function cannot be created.");

        return methodInfo;
    }
}
