# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. This file is intentionally limited to active blockers and known issues.

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- `AzureFunctions.TestFramework.Http` is still a minimal package with little public surface area today; it is packed/published so the package set stays stable while HTTP-specific helpers are added later.

## Resolved issues

- **ALC type-identity mismatches — `FunctionContext` and `HttpRequest` bind as null** — When the Worker SDK's `DefaultMethodInfoLocator.GetMethod()` calls `AssemblyLoadContext.Default.LoadFromAssemblyPath()` during `FunctionLoadRequest` processing, it can load a second copy of the same assembly into the Default ALC, causing two distinct `RuntimeTypeHandle` values for the same type name. The SDK's built-in converters use `context.TargetType == typeof(T)` or `obj is T` checks that silently fail, leaving trigger parameters null. Fixed at the root by replacing `DefaultMethodInfoLocator` with `InProcessMethodInfoLocator` (via `DispatchProxy`), which searches `AppDomain.CurrentDomain.GetAssemblies()` for already-loaded assemblies instead of calling `LoadFromAssemblyPath`. As defense-in-depth, `TestFunctionContextConverter` and `TestHttpRequestConverter` remain registered via `PostConfigure<WorkerOptions>` at index 0 — both compare by `FullName` instead of type identity, and `TestHttpRequestConverter` retrieves `HttpRequest` via reflection (bypassing the failing `is HttpContext` cast).

- **`HttpContextConverter` unable to read `HttpRequest` from `FunctionContext`** — When using the test framework in a project where the function app declares `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, `HttpContextConverter.GetHttpContext()` could silently return `null` (the `as HttpContext` cast fails) because two physical copies of `Microsoft.AspNetCore.Http.Abstractions.dll` could be loaded in the same process — one from the shared framework (functions app) and one via the NuGet package chain (test framework). Fixed by adding an explicit `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `AzureFunctions.TestFramework.Core` and `AzureFunctions.TestFramework.Http.AspNetCore`, ensuring all ASP.NET Core types resolve from the same shared runtime.
