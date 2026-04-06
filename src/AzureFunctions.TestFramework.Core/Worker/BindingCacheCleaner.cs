using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Azure.Functions.Worker.Converters;

namespace AzureFunctions.TestFramework.Core.Worker;

/// <summary>
/// Clears stale entries from the Worker SDK's internal
/// <c>IBindingCache&lt;ConversionResult&gt;</c> to prevent cache-poisoned values from
/// causing <see cref="InvalidCastException"/> during function input binding.
///
/// <para><b>Background</b>: when user middleware (e.g. a correlation middleware) calls
/// <c>FunctionContext.GetHttpRequestDataAsync()</c>, the SDK's
/// <c>DefaultHttpRequestDataFeature</c> invokes <c>BindInputAsync&lt;HttpRequestData&gt;</c>,
/// caching a <c>GrpcHttpRequestData</c> under the HTTP trigger binding name (e.g.
/// <c>"req"</c>).  Later, <c>FunctionExecutionMiddleware</c> calls
/// <c>BindFunctionInputAsync</c> for the same binding name but with
/// <c>TargetType = HttpRequest</c>.  Because the cache key is binding-name-only (no
/// target type), the stale <c>GrpcHttpRequestData</c> is returned and
/// <c>(HttpRequest)GrpcHttpRequestData</c> throws.</para>
///
/// <para>Called from inline middleware registered in
/// <see cref="WorkerHostService.CreateWorkerHostFromApplicationBuilder"/> that runs after
/// user middleware but before <c>FunctionExecutionMiddleware</c>.</para>
/// </summary>
internal static class BindingCacheCleaner
{
    // Cached reflection metadata — populated once on first use.
    private static Type? s_bindingCacheClosedType;
    private static bool s_initialized;
    private static readonly object s_lock = new();

    // Per-implementation-type cache for the ConcurrentDictionary field.
    private static readonly ConcurrentDictionary<Type, FieldInfo?> s_fieldCache = new();

    /// <summary>
    /// Clears the binding cache for the current function invocation scope.
    /// Call this from middleware that runs after user middleware but before
    /// <c>FunctionExecutionMiddleware</c>.
    /// </summary>
    public static void TryClearBindingCache(IServiceProvider services)
    {
        EnsureInitialized();
        if (s_bindingCacheClosedType is null)
            return;

        try
        {
            var cache = services.GetService(s_bindingCacheClosedType);
            if (cache is null)
                return;

            var field = FindDictionaryField(cache.GetType());
            if (field is null)
                return;

            var dict = field.GetValue(cache);
            dict?.GetType().GetMethod("Clear")?.Invoke(dict, null);
        }
        catch
        {
            // Best effort — don't crash if SDK internals change.
        }
    }

    private static void EnsureInitialized()
    {
        if (s_initialized) return;
        lock (s_lock)
        {
            if (s_initialized) return;
            try
            {
                // ConversionResult is public; its assembly is the Worker SDK core assembly.
                var workerAssembly = typeof(ConversionResult).Assembly;

                var cacheOpenType = workerAssembly.GetTypes()
                    .FirstOrDefault(t =>
                        t.IsInterface
                        && t.IsGenericTypeDefinition
                        && t.Name == "IBindingCache`1");

                if (cacheOpenType is not null)
                {
                    s_bindingCacheClosedType =
                        cacheOpenType.MakeGenericType(typeof(ConversionResult));
                }
            }
            catch
            {
                // Best effort — don't crash during startup.
            }
            finally
            {
                s_initialized = true;
            }
        }
    }

    private static FieldInfo? FindDictionaryField(Type implType)
    {
        return s_fieldCache.GetOrAdd(implType, static t =>
            t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
             .FirstOrDefault(static f =>
                 f.FieldType.IsGenericType
                 && f.FieldType.GetGenericTypeDefinition() == typeof(ConcurrentDictionary<,>)));
    }
}
