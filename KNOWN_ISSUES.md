# Known Issues

For current capabilities, package layout, and common commands, see `README.md`. This file is intentionally limited to active blockers and known issues.

## Active blockers

- No active blockers for the current Worker SDK 2.x sample/test suites.

## Known issues

- `FunctionsTestHostBuilder.ConfigureEnvironmentVariable(name, value)` sets process-level environment variables, so tests that need different values for the same variable name should not run in parallel.
- The durable support package currently uses a framework-owned fake path (`ConfigureFakeDurableSupport(...)` + `FunctionsDurableClientProvider`) instead of the real Durable runtime and execution engine.
- `AzureFunctions.TestFramework.Http` is still a minimal package with little public surface area today; it is packed/published so the package set stays stable while HTTP-specific helpers are added later.

## Resolved issues

- **ALC type-identity mismatches (`FunctionContext`, `HttpContext`, `ServiceBusMessage`, etc. bind as null)** — When using the framework in a real-world project, the Worker SDK loads `Microsoft.Azure.Functions.Worker.Extensions.dll` and its dependencies from `.azurefunctions` into an isolated `AssemblyLoadContext` (ALC). If a DLL (e.g. `Azure.Messaging.ServiceBus.dll`, `Worker.Core.dll`) is present in both the main test output directory and `.azurefunctions`, the process contains two distinct `RuntimeTypeHandle` values for the same type name. Casts such as `context as HttpContext` and converter checks like `context.TargetType == typeof(FunctionContext)` fail silently, causing trigger parameters to bind as `null`. Fixed at the root by a MSBuild target (`AzFuncTestFramework_FilterExtensions`, shipped in the NuGet `build/` folder) that deletes the overlapping DLLs from `.azurefunctions` after every build — forcing the isolated ALC to fall back to the default ALC for shared assemblies. A runtime fallback (`GetOrCreateFilteredExtensionRoot`) creates a temp filtered copy in case the build-time step is unavailable. Defense-in-depth fallback `IInputConverter` registrations for `FunctionContext` and `HttpContext`/`HttpRequest`/`HttpResponse` are also registered.

- **Functions without explicit `Route =` not matched** — HTTP trigger functions that omit `Route =` in `[HttpTrigger]` were silently skipped during route registration in `GrpcHostService.HandleFunctionsMetadataResponse`. The code only processed bindings that had a non-null `route` property in the raw binding JSON, causing `SendInvocationRequestAsync` to find no matching function and log "No function found". Fixed by defaulting `route` to `functionMetadata.Name` when the `route` property is absent or null, matching Azure Functions' own built-in default behaviour.

- **`HttpContextConverter` unable to read `HttpRequest` from `FunctionContext`** — When using the test framework in a project where the function app declares `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, `HttpContextConverter.GetHttpContext()` could silently return `null` (the `as HttpContext` cast fails) because two physical copies of `Microsoft.AspNetCore.Http.Abstractions.dll` could be loaded in the same process — one from the shared framework (functions app) and one via the NuGet package chain (test framework). Fixed by adding an explicit `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `AzureFunctions.TestFramework.Core` and `AzureFunctions.TestFramework.Http.AspNetCore`, ensuring all ASP.NET Core types resolve from the same shared runtime.
