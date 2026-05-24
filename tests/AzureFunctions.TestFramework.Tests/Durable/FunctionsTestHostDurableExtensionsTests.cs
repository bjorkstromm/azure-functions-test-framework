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

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_NullHost_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).InvokeActivityAsync<string>("SomeActivity", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_EmptyFunctionName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.InvokeActivityAsync<string>(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_WhitespaceFunctionName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.InvokeActivityAsync<string>("   ", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task InvokeActivityAsync_WithoutDurableSupport_ThrowsInvalidOperationException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.InvokeActivityAsync<string>("SomeActivity", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("ConfigureFakeDurableSupport", ex.Message);
    }

    // ── FunctionsTestHostDurableEntityExtensions — SignalEntityAsync ──────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task SignalEntityAsync_NullHost_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).SignalEntityAsync(TestEntityId, "op", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task SignalEntityAsync_EmptyOperationName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.SignalEntityAsync(TestEntityId, string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task SignalEntityAsync_WithoutDurableSupport_ThrowsInvalidOperationException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.SignalEntityAsync(TestEntityId, "add", 5, TestContext.Current.CancellationToken));

        Assert.Contains("ConfigureFakeDurableSupport", ex.Message);
    }

    // ── FunctionsTestHostDurableEntityExtensions — CallEntityAsync ────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task CallEntityAsync_NullHost_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).CallEntityAsync<int>(TestEntityId, "get", cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task CallEntityAsync_EmptyOperationName_ThrowsArgumentException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            host.CallEntityAsync<int>(TestEntityId, string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task CallEntityAsync_WithoutDurableSupport_ThrowsInvalidOperationException()
    {
        var host = CreateStubHost(withDurableSupport: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.CallEntityAsync<int>(TestEntityId, "get", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("ConfigureFakeDurableSupport", ex.Message);
    }

    // ── FunctionsTestHostDurableEntityExtensions — GetEntity ─────────────────

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public void GetEntity_NullHost_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IFunctionsTestHost)null!).GetEntity<int>(TestEntityId));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
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
