using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for the guard conditions (null-argument and missing-service checks) in
/// <see cref="FunctionsTestHostDurableActivityExtensions"/> and
/// <see cref="FunctionsTestHostDurableEntityExtensions"/>.
/// </summary>
public class FunctionsTestHostDurableExtensionsTests
{
    private static readonly EntityInstanceId TestEntityId = new("TestEntity", "key1");

    // ── FunctionsTestHostDurableActivityExtensions ────────────────────────────

    [Fact]
    public async Task InvokeActivityAsync_NullHost_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).InvokeActivityAsync<string>("SomeActivity"));
    }

    [Fact]
    public async Task InvokeActivityAsync_EmptyFunctionName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.InvokeActivityAsync<string>(string.Empty));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task InvokeActivityAsync_WhitespaceFunctionName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.InvokeActivityAsync<string>("   "));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task InvokeActivityAsync_WithoutDurableSupport_ThrowsInvalidOperationException()
    {
        var host = CreateStubHost(withDurableSupport: false);

#pragma warning disable xUnit1051
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.InvokeActivityAsync<string>("SomeActivity"));
#pragma warning restore xUnit1051

        Assert.Contains("ConfigureFakeDurableSupport", ex.Message);
    }

    // ── FunctionsTestHostDurableEntityExtensions — SignalEntityAsync ──────────

    [Fact]
    public async Task SignalEntityAsync_NullHost_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).SignalEntityAsync(TestEntityId, "op"));
    }

    [Fact]
    public async Task SignalEntityAsync_EmptyOperationName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.SignalEntityAsync(TestEntityId, string.Empty));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task SignalEntityAsync_WithoutDurableSupport_ThrowsInvalidOperationException()
    {
        var host = CreateStubHost(withDurableSupport: false);

#pragma warning disable xUnit1051
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.SignalEntityAsync(TestEntityId, "add", 5));
#pragma warning restore xUnit1051

        Assert.Contains("ConfigureFakeDurableSupport", ex.Message);
    }

    // ── FunctionsTestHostDurableEntityExtensions — CallEntityAsync ────────────

    [Fact]
    public async Task CallEntityAsync_NullHost_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).CallEntityAsync<int>(TestEntityId, "get"));
    }

    [Fact]
    public async Task CallEntityAsync_EmptyOperationName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

#pragma warning disable xUnit1051
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.CallEntityAsync<int>(TestEntityId, string.Empty));
#pragma warning restore xUnit1051
    }

    [Fact]
    public async Task CallEntityAsync_WithoutDurableSupport_ThrowsInvalidOperationException()
    {
        var host = CreateStubHost(withDurableSupport: false);

#pragma warning disable xUnit1051
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.CallEntityAsync<int>(TestEntityId, "get"));
#pragma warning restore xUnit1051

        Assert.Contains("ConfigureFakeDurableSupport", ex.Message);
    }

    // ── FunctionsTestHostDurableEntityExtensions — GetEntity ─────────────────

    [Fact]
    public void GetEntity_NullHost_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).GetEntity<int>(TestEntityId));
    }

    [Fact]
    public void GetEntity_WithoutDurableSupport_ThrowsInvalidOperationException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            host.GetEntity<int>(TestEntityId));

        Assert.Contains("ConfigureFakeDurableSupport", ex.Message);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IFunctionsTestHost CreateStubHost(bool withDurableSupport)
    {
        _ = withDurableSupport; // reserved for future use
        // Always returns a service provider without durable services registered.
        var services = new ServiceCollection().BuildServiceProvider();
        return new StubFunctionsTestHost(services);
    }

    private sealed class StubFunctionsTestHost : IFunctionsTestHost
    {
        public StubFunctionsTestHost(IServiceProvider services) => Services = services;

        public IServiceProvider Services { get; }
        public IFunctionInvoker Invoker => null!;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
