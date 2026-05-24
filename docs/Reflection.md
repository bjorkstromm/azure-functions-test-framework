# Reflection Usage in Azure Functions Test Framework

This document catalogs every place where .NET reflection is used in the test framework, explains _why_ each usage exists, and describes what changes to the Azure Functions Worker SDK would make that reflection unnecessary.

---

## Summary Table

| Component | File | What is reflected | Why |
|---|---|---|---|
| [InProcessMethodInfoLocator](#1-inprocessmethodinfolocator) | `Core/Worker/InProcessMethodInfoLocator.cs` | Internal `IMethodInfoLocator` interface + `DispatchProxy` | Replace SDK default that calls `LoadFromAssemblyPath` |
| [InMemoryWorkerClientFactory](#2-inmemorygrpcclientfactory--inmemorygrpcworkerclient) | `Core/Grpc/InMemoryWorkerClientFactory.cs` | Internal `IWorkerClientFactory`, `IWorkerClient`, `IMessageProcessor`, `FunctionRpc.FunctionRpcClient`, `StartStream`, `StreamingMessage` | Route gRPC traffic in-memory through `TestServer` |
| [BindingCacheCleaner](#3-bindingcachecleaner) | `Core/Worker/BindingCacheCleaner.cs` | Internal `IBindingCache<T>` interface + private `ConcurrentDictionary` field | Clear stale cache entries to avoid `InvalidCastException` |
| [TestHttpRequestConverter](#4-testhttprequestconverter) | `Core/Worker/Converters/TestHttpRequestConverter.cs` | `HttpContext.Request` property via string-based lookup | Bypass ALC type-identity check when resolving `HttpRequest` |
| [IAutoConfigureStartup scan](#5-iautoconfigurestartup-scan) | `Core/Worker/WorkerHostService.cs` | Assembly scan for source-generated `IAutoConfigureStartup` implementations | Register generated function metadata and executor providers |
| [FunctionsTestHostBuilderServiceBusExtensions](#6-servicebus-fake-converter-interception) | `ServiceBus/FunctionsTestHostBuilderServiceBusExtensions.cs` | Internal `ServiceBusMessageActionsConverter`, `ServiceBusSessionMessageActionsConverter` types by name | Register fake settlement converters without requiring the real gRPC settlement channel |
| [FunctionsTestHostBuilderDurableExtensions](#7-durable-internals) | `Durable/FunctionsTestHostBuilderDurableExtensions.cs` | Internal `DurableTaskClientConverter`, `FunctionsDurableClientProvider` + nested `ClientKey` / `ClientHolder` types | Inject fake `DurableTaskClient` without the real gRPC binding payload |
| [FakeDurableFunctionCatalog](#8-fakedurablefunctioncatalog) | `Durable/FakeDurableFunctionCatalog.cs` | Public attributes (`[Function]`, `[ActivityTrigger]`, etc.) via `GetCustomAttribute` | Discover durable activities/orchestrators/entities without a `functions.metadata` file |
| [FakeDurableOrchestrationRunner](#9-fakedurableorchestrationrunner) | `Durable/FakeDurableOrchestrationRunner.cs` | `MethodInfo.Invoke`, `Task<T>.Result`, `ValueTask<T>.AsTask()` | Execute durable function methods (activities, orchestrators) directly |
| [WorkerExtensionStartup invocation](#10-workerextensionstartup-invocation) | `Core/Worker/WorkerHostService.cs` | Public `WorkerExtensionStartupCodeExecutorInfoAttribute` + `Activator.CreateInstance` | Invoke extension startup from functions assembly (SDK reads wrong entry assembly in test runners) |
| [Pipeline middleware reordering](#11-pipeline-middleware-reordering) | `Core/Worker/WorkerHostService.cs` | Internal `FunctionsWorkerApplicationBuilder._pipelineBuilder._middlewareCollection` fields | Insert extension middleware before default middleware in IHostBuilder + factory path |

---

## Detailed Descriptions

### 1. InProcessMethodInfoLocator

**File:** `src/AzureFunctions.TestFramework.Core/Worker/InProcessMethodInfoLocator.cs`

**SDK types reflected:**
- `Microsoft.Azure.Functions.Worker.Invocation.IMethodInfoLocator` (internal interface)
  - Method: `MethodInfo GetMethod(string pathToAssembly, string entryPoint)`

**Why reflection is needed:**

The Worker SDK's default implementation, `DefaultMethodInfoLocator`, resolves function entry-point methods by calling `AssemblyLoadContext.Default.LoadFromAssemblyPath(pathToAssembly)`. In an in-process test setup, the function assembly is already loaded by the test runner. A second load via `LoadFromAssemblyPath` introduces a second copy of the same assembly into the Default ALC, causing type-identity mismatches (`typeof(T) ==` and `obj is T` fail silently). The framework replaces `IMethodInfoLocator` with a `DispatchProxy`-based implementation that searches already-loaded assemblies first.

Because `IMethodInfoLocator` is **internal**, it cannot be implemented directly. The `DispatchProxy.Create<TInterface, TProxy>()` method is called via reflection to obtain the correct closed generic, and the proxy is registered with `services.AddSingleton(locatorInterface, proxy)`.

**How to avoid reflection:**

Make `IMethodInfoLocator` **public** and add an official extension point — for example:
```csharp
// Hypothetical Worker SDK API
workerBuilder.UseMethodInfoLocator<MyLocator>();
// or:
services.AddSingleton<IMethodInfoLocator, MyLocator>();
```
Any of these would allow the framework to implement and register the interface without reflection.

---

### 2. InMemoryGrpcClientFactory / InMemoryGrpcWorkerClient

**File:** `src/AzureFunctions.TestFramework.Core/Grpc/InMemoryWorkerClientFactory.cs`

**SDK types reflected (all internal to `Microsoft.Azure.Functions.Worker.Grpc`):**
- `IWorkerClientFactory` — method: `object CreateClient(object processor)`
- `IWorkerClient` — methods: `Task StartAsync(CancellationToken)`, `ValueTask SendMessageAsync(StreamingMessage)`
- `IMessageProcessor` — method: `Task ProcessMessageAsync(StreamingMessage)`
- `FunctionRpc+FunctionRpcClient` (nested type) — method: `AsyncDuplexStreamingCall<StreamingMessage,StreamingMessage> EventStream(Metadata, DateTime?, CancellationToken)`
- `StartStream` message type — property: `string WorkerId`
- `StreamingMessage` — property: `StartStream StartStream`

**Why reflection is needed:**

The Worker SDK communicates with the host over a bidirectional gRPC `EventStream`. For in-process testing with no TCP sockets, the framework must intercept the moment the SDK creates its gRPC channel and redirect traffic through a `TestServer`-backed in-memory `HttpMessageHandler`.

The SDK's gRPC plumbing is entirely internal: `IWorkerClientFactory`, `IWorkerClient`, and `IMessageProcessor` are not public. The framework therefore creates `DispatchProxy`-based implementations for each interface. The `DispatchProxy.Create<T, TProxy>()` overload requires knowing the interface type at compile time — since the types are internal, the generic call is constructed via reflection. Property access on gRPC call objects (`RequestStream`, `ResponseStream`, `WriteAsync`, `MoveNextAsync`, `Current`) is also reflection-based because all types are opaque.

**How to avoid reflection:**

The minimal change would be to make the following types and members **public**:

```csharp
// Microsoft.Azure.Functions.Worker.Grpc
public interface IWorkerClientFactory
{
    IWorkerClient CreateClient(IMessageProcessor processor);
}

public interface IWorkerClient
{
    Task StartAsync(CancellationToken cancellationToken);
    ValueTask SendMessageAsync(StreamingMessage message);
}

public interface IMessageProcessor
{
    Task ProcessMessageAsync(StreamingMessage message);
}
```

With public interfaces, the framework can implement them directly without `DispatchProxy`. The `FunctionRpcClient.EventStream` method is already public (generated by the Grpc.Tools protobuf compiler). Access to `RequestStream` / `ResponseStream` would no longer need reflection once the framework calls `EventStream` with a typed return.

Alternatively, the Worker SDK could expose a **testing entry point** analogous to ASP.NET Core's `WebApplicationFactory`:

```csharp
// Hypothetical
hostBuilder.UseInMemoryWorkerTransport(HttpMessageHandler handler);
```

---

### 3. BindingCacheCleaner

**File:** `src/AzureFunctions.TestFramework.Core/Worker/BindingCacheCleaner.cs`

**SDK types reflected:**
- `IBindingCache<T>` — internal generic interface in `Microsoft.Azure.Functions.Worker.Core`
- The private `ConcurrentDictionary<,>` field inside the concrete implementation of `IBindingCache<ConversionResult>`

**Why reflection is needed:**

When user middleware calls `FunctionContext.GetHttpRequestDataAsync()`, the SDK's `DefaultHttpRequestDataFeature` populates `IBindingCache<ConversionResult>` with the raw `GrpcHttpRequestData`. Later, `FunctionExecutionMiddleware` looks up the same cache key expecting an `HttpRequest` (in ASP.NET Core integration mode). The stale cache entry causes an `InvalidCastException`.

The framework clears the cache from middleware that runs after user middleware but before `FunctionExecutionMiddleware`. Because `IBindingCache<T>` is internal and its concrete implementation is sealed with a private dictionary field, the only way to reach the cache is:
1. Resolve the closed `IBindingCache<ConversionResult>` type via reflection.
2. Find the `ConcurrentDictionary` private field on the resolved instance.
3. Call `Clear()` on it.

**How to avoid reflection:**

Make `IBindingCache<T>` public and expose a `Clear()` method (or `Remove(string bindingName)`):

```csharp
// Hypothetical Worker SDK API
public interface IBindingCache<T>
{
    bool TryGet(string key, out T value);
    void Set(string key, T value);
    void Remove(string key);   // <-- needed
    void Clear();               // <-- needed
}
```

With a public `Clear()` or `Remove()`, the framework can call it directly without walking the private field.

Alternatively, the SDK could expose a scoped `IBindingCacheScope` that is automatically reset between invocations:

```csharp
// Hypothetical — cache is per-invocation scope, automatically cleared
services.AddScoped<IBindingCache<ConversionResult>, PerInvocationBindingCache>();
```

---

### 4. TestHttpRequestConverter

**File:** `src/AzureFunctions.TestFramework.Core/Worker/Converters/TestHttpRequestConverter.cs`

**SDK types reflected:**
- `HttpContext.Request` property — accessed by name (`"Request"`) via `GetType().GetProperty("Request")`

**Why reflection is needed:**

In ASP.NET Core integration mode, the SDK stores an `HttpContext` object inside `FunctionContext.Items["HttpRequestContext"]`. A subsequent input converter must extract `HttpContext.Request` from it. The framework uses a string-based `GetProperty("Request")` call to be immune to ALC type-identity issues: even if `HttpContext` is loaded from two different assembly load contexts (and `obj is HttpContext` returns `false`), the property name `"Request"` is stable.

**How to avoid reflection:**

Make the ASP.NET Core feature extraction public. For example, the Worker SDK already exposes `FunctionsHttpProxyingMiddleware` which stores an `AspNetCoreHttpRequestDataFeature` in `FunctionContext.Features`. If this feature were publicly documented and stable, test frameworks could access `HttpContext` through the features API rather than through `Items`:

```csharp
var feature = functionContext.Features.Get<IHttpContextFeature>();
var httpContext = feature?.HttpContext; // typed, no reflection needed
```

---

### 5. IAutoConfigureStartup Scan

**File:** `src/AzureFunctions.TestFramework.Core/Worker/WorkerHostService.cs`

**Assembly members scanned:**
- All types in the functions assembly implementing `IAutoConfigureStartup` (a public SDK interface)
- No private members accessed — uses `typeof(IAutoConfigureStartup).IsAssignableFrom(t)` + `Activator.CreateInstance`

**Why reflection is needed:**

The Worker SDK source generator emits `FunctionMetadataProviderAutoStartup` and `FunctionExecutorAutoStartup` classes that implement `IAutoConfigureStartup`. In a normal process startup, the SDK discovers them via a configured host extension. In in-process testing, the `IHostBuilder` or `FunctionsApplicationBuilder` created by the test framework does not automatically trigger these registrations unless the startup implementations are discovered and invoked.

Technically `IAutoConfigureStartup` is **public**, so this usage does not depend on any private SDK internals. It is an assembly scan for a known-public interface. The only "reflection" is `Activator.CreateInstance(type)`, which is standard .NET practice for plugin/startup discovery.

**How to avoid reflection:**

No change is strictly needed here — `IAutoConfigureStartup` is public. If the SDK exposed an explicit method to register auto-startup implementations:

```csharp
// Hypothetical
hostBuilder.RegisterGeneratedStartups(functionsAssembly);
```

…the assembly scan could be replaced with a single deterministic call.

---

### 6. ServiceBus Fake Converter Interception

**File:** `src/AzureFunctions.TestFramework.ServiceBus/FunctionsTestHostBuilderServiceBusExtensions.cs`

**SDK types reflected (internal to `Microsoft.Azure.Functions.Worker.Extensions.ServiceBus`):**
- `ServiceBusMessageActionsConverter` — by full qualified name via `Assembly.GetType(...)`
- `ServiceBusSessionMessageActionsConverter` — by full qualified name via `Assembly.GetType(...)`

**Why reflection is needed:**

When a function declares a `ServiceBusMessageActions` or `ServiceBusSessionMessageActions` parameter, the Worker SDK resolves the value through its own internal converters (`ServiceBusMessageActionsConverter` / `ServiceBusSessionMessageActionsConverter`). These converters hold a reference to `Settlement.SettlementClient`, a live gRPC channel to the real Azure Service Bus settlement endpoint. In an in-process test environment, that gRPC channel does not exist.

The framework intercepts these converters by registering the fake implementations under the real internal converter types in the worker's DI container. When the SDK calls `ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, converterType)`, it finds the fakes in the container instead of constructing the real converters.

Because both converter types are **internal**, their `System.Type` references cannot be obtained at compile time. The framework retrieves them via `typeof(ServiceBusTriggerAttribute).Assembly.GetType("...", throwOnError: true)` — a string-based lookup against the known assembly.

**How to avoid reflection:**

Expose the converter types publicly, or provide a dedicated testing registration API:

```csharp
// Hypothetical Microsoft.Azure.Functions.Worker.Extensions.ServiceBus.Testing namespace
public static class ServiceBusTestingExtensions
{
    public static IServiceCollection AddFakeServiceBusMessageActions(
        this IServiceCollection services,
        ServiceBusMessageActions fakeActions);

    public static IServiceCollection AddFakeServiceBusSessionMessageActions(
        this IServiceCollection services,
        ServiceBusSessionMessageActions fakeActions);
}
```

---

### 7. Durable Internals

**File:** `src/AzureFunctions.TestFramework.Durable/FunctionsTestHostBuilderDurableExtensions.cs`

**SDK types reflected (all internal to `Microsoft.Azure.Functions.Worker.Extensions.DurableTask`):**
- `DurableTaskClientConverter` — internal input converter type
- `FunctionsDurableClientProvider` — internal provider type
  - Private field: `clients` (`IDictionary`)
  - Private nested type: `ClientKey` (constructor: `(Uri endpoint, string taskHub, string connectionName)`)
  - Private nested type: `ClientHolder` (constructor: `(DurableTaskClient client, object? secondArg)`)

**Why reflection is needed:**

The Worker SDK's durable input converter (`DurableTaskClientConverter`) resolves a `DurableTaskClient` by reading a JSON binding payload containing `rpcBaseUrl`, `taskHubName`, and `connectionName`. This payload is only present when a real Azure Functions host sends the gRPC `InvocationRequest`. In in-process testing, no such payload is present and the converter fails.

The framework injects a fake `DurableTaskClient` by:
1. Getting the `DurableTaskClientConverter` type by name (to register `FakeDurableTaskClientInputConverter` as that type in DI, so `ActivatorUtilities.GetServiceOrCreateInstance` returns the fake).
2. Getting `FunctionsDurableClientProvider` by name (to pre-populate its internal `clients` dictionary with `FakeDurableTaskClient` keyed by the binding's `(endpoint, taskHub, connectionName)` tuple).

Both types and the `ClientKey`/`ClientHolder` nested types are internal, requiring full reflection to construct and manipulate them.

**How to avoid reflection:**

The Durable extensions package could expose testing helpers:

```csharp
// Hypothetical Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Testing namespace
public static class DurableTestingExtensions
{
    // Register a custom DurableTaskClient for testing
    public static IServiceCollection AddTestDurableClient(
        this IServiceCollection services,
        DurableTaskClient client,
        string taskHub = "",
        string connectionName = "");
}
```

Alternatively, exposing `IFunctionsDurableClientProvider` publicly with a `Register(DurableTaskClient client, string taskHub, string connectionName)` method would allow registering fake clients without reflection.

---

### 8. FakeDurableFunctionCatalog

**File:** `src/AzureFunctions.TestFramework.Durable/FakeDurableFunctionCatalog.cs`

**What is reflected:**
- `Assembly.GetTypes()` — standard reflection to enumerate all types
- `type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)` — scan for durable trigger attributes
- `method.GetCustomAttribute<FunctionAttribute>()` — all attributes involved are **public**
- `method.GetCustomAttribute<ActivityTriggerAttribute>()`, `OrchestrationTriggerAttribute`, `EntityTriggerAttribute`

**Why reflection is needed:**

The catalog must discover which methods in the functions assembly are durable activities, orchestrators, and entities without a running host. The `[Function]`, `[ActivityTrigger]`, `[OrchestrationTrigger]`, and `[EntityTrigger]` attributes are all public, so this is standard attribute discovery via `GetCustomAttribute`. No private SDK internals are accessed.

**How to avoid reflection:**

No SDK change is needed. However, if the Worker SDK's source generator emitted a public catalog class (analogous to `GeneratedFunctionMetadataProvider`), the scan could be replaced with a direct call:

```csharp
// Hypothetical source-generated class
var catalog = new GeneratedDurableFunctionCatalog();
foreach (var activity in catalog.Activities) { ... }
```

---

### 9. FakeDurableOrchestrationRunner

**File:** `src/AzureFunctions.TestFramework.Durable/FakeDurableOrchestrationRunner.cs`

**What is reflected:**
- `MethodInfo.Invoke(target, arguments)` — invoke activity/orchestrator methods
- `method.ReturnType.GetProperty("Result")?.GetValue(result)` — unwrap `Task<T>.Result`
- `method.ReturnType.GetMethod("AsTask")?.Invoke(result, ...)` — convert `ValueTask<T>` to `Task`
- `method.GetParameters()` — inspect parameter list to inject `FunctionContext`, `CancellationToken`, trigger values
- `parameter.GetCustomAttributes()` — detect trigger attributes (all public)

**Why reflection is needed:**

The runner invokes durable function methods generically without knowing their signatures at compile time. Because activities and orchestrators can have any return type (`Task`, `Task<T>`, `ValueTask<T>`, plain `T`), it must unwrap the result reflectively. This is a fundamental consequence of the dynamic dispatch design and does not require any private SDK internals.

**How to avoid reflection:**

No SDK change is strictly needed. The reflection here is within the framework's own fake implementation, not against SDK internals. It could be partially replaced using source generation or by requiring function authors to register their durable functions explicitly:

```csharp
// Hypothetical opt-in registration
builder.RegisterDurableActivity<MyActivities>(nameof(MyActivities.ProcessItem));
```

However, this would be a breaking change in the framework's test authoring experience and is not recommended unless performance is a concern.

---

### 10. WorkerExtensionStartup Invocation

**File:** `src/AzureFunctions.TestFramework.Core/Worker/WorkerHostService.cs`

**SDK types used:**
- `WorkerExtensionStartupCodeExecutorInfoAttribute` (public, `Microsoft.Azure.Functions.Worker.Core`) — `Assembly.GetCustomAttribute<WorkerExtensionStartupCodeExecutorInfoAttribute>()`
- `WorkerExtensionStartupCodeExecutorInfoAttribute.StartupCodeExecutorType` property (public)
- `WorkerExtensionStartup` (public abstract class, `Microsoft.Azure.Functions.Worker.Core`) — base class of the source-generated executor
- `WorkerExtensionStartup.Configure(IFunctionsWorkerApplicationBuilder)` (public virtual)
- `Activator.CreateInstance(attr.StartupCodeExecutorType)` — instantiate the source-generated executor

**Why reflection is needed:**

The Worker SDK's `RunExtensionStartupCode` reads `Assembly.GetEntryAssembly()` to find the `WorkerExtensionStartupCodeExecutorInfoAttribute`. In a test-runner process, `GetEntryAssembly()` returns the test runner (e.g. xUnit), not the functions assembly. As a result, extension middleware registered via `WorkerExtensionStartup.Configure()` — such as MCP's `FunctionsMcpContextMiddleware` — is never invoked.

The framework reads the attribute from the **functions assembly** (passed via `WithFunctionsAssembly`) and invokes `Configure(builder)` directly. All types involved are **public**, so no private SDK internals are accessed. The only "reflection" is `Activator.CreateInstance(type)`, which is standard .NET plugin discovery.

**How to avoid reflection:**

No change is strictly needed — all types are public. If the SDK exposed an explicit method:

```csharp
// Hypothetical
builder.RunExtensionStartupCode(functionsAssembly);
```

…the `Activator.CreateInstance` call could be replaced with a direct SDK API call.

---

### 11. Pipeline Middleware Reordering

**File:** `src/AzureFunctions.TestFramework.Core/Worker/WorkerHostService.cs`

**SDK types reflected (all internal to `Microsoft.Azure.Functions.Worker.Core`):**
- `FunctionsWorkerApplicationBuilder` (internal concrete class)
  - Private field: `_pipelineBuilder` (`IInvocationPipelineBuilder<FunctionContext>`)
- `DefaultInvocationPipelineBuilder<FunctionContext>` (internal concrete class)
  - Private field: `_middlewareCollection` (`IList<Func<FunctionExecutionDelegate, FunctionExecutionDelegate>>`)

**Why reflection is needed:**

In the IHostBuilder + factory path, the user's factory calls `ConfigureFunctionsWorkerDefaults(b => { ... })` which adds the user's middleware and then internally appends `OutputBindingsMiddleware` and `FunctionExecutionMiddleware` via `UseDefaultWorkerMiddleware`. The framework invokes extension startup code **after** this (via `ConfigureServices`), so any extension middleware is appended **after** `FunctionExecutionMiddleware`.

The `DefaultInvocationPipelineBuilder.Build()` reverses the list and folds — meaning the last-added middleware becomes the innermost. Extension middleware like `FunctionsMcpContextMiddleware` must run **before** `FunctionExecutionMiddleware` to populate `FunctionContext.Items` before the function body executes.

The framework accesses the internal `_middlewareCollection` field via reflection to **insert** newly registered extension middleware at position 0 (front of the list), ensuring it runs before the default middleware.

**How to avoid reflection:**

Expose a public API on `IFunctionsWorkerApplicationBuilder` for prepending middleware:

```csharp
// Hypothetical
builder.UseMiddlewareAtFront<FunctionsMcpContextMiddleware>();
// or:
builder.Insert(0, next => new McpMiddleware(next).Invoke);
```

Alternatively, change `RunExtensionStartupCode` to accept a target assembly parameter, eliminating the need for the framework to invoke it separately:

```csharp
// Hypothetical
builder.RunExtensionStartupCode(functionsAssembly);
```

---

## Worker SDK Changes That Would Eliminate All Reflection

The following is a prioritized list of changes to the Azure Functions Worker SDK that would allow this test framework to remove all reflection against SDK internals.

### High Priority (would eliminate the most complex reflection)

1. **Make `IWorkerClientFactory`, `IWorkerClient`, `IMessageProcessor` public** (`Microsoft.Azure.Functions.Worker.Grpc`)
   - Eliminates all `DispatchProxy`-based interception in `InMemoryWorkerClientFactory.cs`
   - Estimated impact: removes ~150 lines of fragile reflection code

2. **Make `IMethodInfoLocator` public** (`Microsoft.Azure.Functions.Worker.Invocation` / `Microsoft.Azure.Functions.Worker`)
   - Eliminates `InProcessMethodInfoLocator`'s `DispatchProxy` wrapper
   - Estimated impact: removes ~30 lines; `MethodInfoLocatorProxy` becomes a simple `class : IMethodInfoLocator`

3. **Expose durable testing helpers** (`Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Testing`)
   - A public `AddTestDurableClient(services, fakeClient, ...)` extension method
   - Eliminates all reflection in `FunctionsTestHostBuilderDurableExtensions.RegisterInternalDurableClientProvider`
   - Estimated impact: removes ~60 lines of fragile reflection against `FunctionsDurableClientProvider` internals

### Medium Priority

4. **Expose Service Bus testing helpers** (`Microsoft.Azure.Functions.Worker.Extensions.ServiceBus.Testing`)
   - `AddFakeServiceBusMessageActions(services, fakeActions)` and `AddFakeServiceBusSessionMessageActions(services, fakeActions)` extension methods
   - Eliminates the string-based `Assembly.GetType(...)` lookup for internal converter types in `FunctionsTestHostBuilderServiceBusExtensions.cs`
   - Estimated impact: removes ~10 lines

5. **Make `IBindingCache<T>` public and add `Clear()` / `Remove(string)`**
   - Eliminates `BindingCacheCleaner`'s private-field reflection
   - Estimated impact: removes ~30 lines

6. **Expose `IHttpContextFeature` / `IHttpRequestDataFeature` in `FunctionContext.Features`** (publicly documented)
   - Eliminates `TestHttpRequestConverter`'s `GetProperty("Request")` fallback
   - Estimated impact: removes ~10 lines

### Lower Priority

7. **Source-generated durable function catalog** — `GeneratedDurableFunctionCatalog` class
   - Would replace `FakeDurableFunctionCatalog`'s assembly scan with a direct lookup
   - Not strictly needed as all attributes scanned are already public

8. **`RegisterGeneratedStartups(Assembly)` host builder extension**
   - Would replace the `IAutoConfigureStartup` assembly scan in `WorkerHostService.cs`
   - Not strictly needed as `IAutoConfigureStartup` is already public

9. **`RunExtensionStartupCode(Assembly)` or `UseMiddlewareAtFront<T>()`**
   - Would replace the `WorkerExtensionStartup` invocation in `WorkerHostService.InvokeExtensionStartupCode` and the pipeline `_middlewareCollection` reflection in `InvokeExtensionStartupCodeAtFront`
   - The startup invocation itself only uses public types (`WorkerExtensionStartupCodeExecutorInfoAttribute`, `WorkerExtensionStartup`, `Activator.CreateInstance`), but the middleware reordering depends on internal fields `_pipelineBuilder` and `_middlewareCollection`
   - Estimated impact: removes ~60 lines of reflection in `GetPipelineMiddlewareList` + `InvokeExtensionStartupCodeAtFront`

---

## Reflection Stability Contract Tests

The test project `tests/AzureFunctions.TestFramework.ReflectionTests` contains contract tests that verify all reflected SDK members are still present whenever the Worker SDK packages are updated. Each test corresponds to one item in this document. If any test fails after a package upgrade, the framework's reflection code must be updated before the upgrade can be merged.

See [`tests/AzureFunctions.TestFramework.ReflectionTests/`](../tests/AzureFunctions.TestFramework.ReflectionTests) for the full test suite.
