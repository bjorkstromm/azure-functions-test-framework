using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="InMemoryGrpcClientFactory"/> helper methods.
/// </summary>
public class InMemoryGrpcClientFactoryTests
{
    private const string WorkerGrpcAssemblyName = "Microsoft.Azure.Functions.Worker.Grpc";

    // Force the gRPC assembly to load so TryGetWorkerGrpcAssembly tests can find it.
    static InMemoryGrpcClientFactoryTests()
    {
        _ = typeof(WorkerOptions).Assembly.FullName;
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == WorkerGrpcAssemblyName))
        {
            var workerDir = Path.GetDirectoryName(typeof(WorkerOptions).Assembly.Location)!;
            var grpcPath = Path.Combine(workerDir, WorkerGrpcAssemblyName + ".dll");
            if (File.Exists(grpcPath))
                Assembly.LoadFrom(grpcPath);
        }
    }

    // ── TryGetWorkerGrpcAssembly ─────────────────────────────────────────────

    [Fact]
    public void TryGetWorkerGrpcAssembly_WhenLoaded_ReturnsTrueAndAssembly()
    {
        var logger = NullLogger.Instance;
        var result = InMemoryGrpcClientFactory.TryGetWorkerGrpcAssembly(logger, out var asm);

        Assert.True(result);
        Assert.NotNull(asm);
        Assert.Equal(WorkerGrpcAssemblyName, asm.GetName().Name);
    }

    // ── TryGetRequiredTypes ──────────────────────────────────────────────────

    [Fact]
    public void TryGetRequiredTypes_WithWorkerGrpcAssembly_ReturnsTrueAndTypes()
    {
        var workerGrpcAsm = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == WorkerGrpcAssemblyName);
        var logger = NullLogger.Instance;

        var result = InMemoryGrpcClientFactory.TryGetRequiredTypes(
            workerGrpcAsm, logger,
            out var factoryInterface, out var clientInterface,
            out var processorInterface, out var functionRpcClientType);

        Assert.True(result);
        Assert.NotNull(factoryInterface);
        Assert.NotNull(clientInterface);
        Assert.NotNull(processorInterface);
        Assert.NotNull(functionRpcClientType);
    }

    [Fact]
    public void TryGetRequiredTypes_WithUnrelatedAssembly_ReturnsFalse()
    {
        // Use the test assembly itself — it has none of the required types
        var unrelatedAsm = typeof(InMemoryGrpcClientFactoryTests).Assembly;
        var logger = NullLogger.Instance;

        var result = InMemoryGrpcClientFactory.TryGetRequiredTypes(
            unrelatedAsm, logger,
            out _, out _, out _, out _);

        Assert.False(result);
    }

    // ── TryGetRequiredMethods ────────────────────────────────────────────────

    [Fact]
    public void TryGetRequiredMethods_WithValidTypes_ReturnsTrueAndMethods()
    {
        var workerGrpcAsm = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == WorkerGrpcAssemblyName);
        InMemoryGrpcClientFactory.TryGetRequiredTypes(
            workerGrpcAsm, NullLogger.Instance,
            out _, out _, out var processorInterface, out var functionRpcClientType);

        var result = InMemoryGrpcClientFactory.TryGetRequiredMethods(
            functionRpcClientType, processorInterface, NullLogger.Instance,
            out var eventStreamMethod, out var processMessageAsync);

        Assert.True(result);
        Assert.NotNull(eventStreamMethod);
        Assert.NotNull(processMessageAsync);
    }

    [Fact]
    public void TryGetRequiredMethods_WithTypeWithoutEventStream_ReturnsFalse()
    {
        // processorInterface exists but the functionRpcClientType is wrong (no EventStream)
        var workerGrpcAsm = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == WorkerGrpcAssemblyName);
        InMemoryGrpcClientFactory.TryGetRequiredTypes(
            workerGrpcAsm, NullLogger.Instance,
            out _, out _, out var processorInterface, out _);

        // Use a type that has no EventStream method
        var noEventStreamType = typeof(object);
        var result = InMemoryGrpcClientFactory.TryGetRequiredMethods(
            noEventStreamType, processorInterface, NullLogger.Instance,
            out _, out _);

        Assert.False(result);
    }

    // ── TryRegister ──────────────────────────────────────────────────────────

    [Fact]
    public void TryRegister_WithRealWorkerGrpcAssembly_ReturnsTrue()
    {
        var services = new ServiceCollection();
        var handler = new FakeHttpMessageHandler();
        var logger = NullLogger.Instance;

        var result = InMemoryGrpcClientFactory.TryRegister(services, handler, logger);

        Assert.True(result);
    }

    /// <summary>Minimal HttpMessageHandler for test use.</summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
