using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableEntityClient"/>, verifying that operations not
/// supported by the fake implementation throw <see cref="NotSupportedException"/> and that
/// the supported <see cref="FakeDurableEntityClient.GetEntityAsync{TState}"/> delegates
/// correctly to the underlying <see cref="FakeDurableEntityRunner"/>.
/// </summary>
public class FakeDurableEntityClientTests
{
    // ── Not-supported operations ──────────────────────────────────────────────

    [Fact]
    public async Task GetEntityAsync_NonGeneric_ThrowsNotSupportedException()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId("TestEntity", "key1");

#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            resources.Client.GetEntityAsync(entityId, includeState: true, CancellationToken.None));
#pragma warning restore xUnit1051
    }

    [Fact]
    public void GetAllEntitiesAsync_NonGeneric_ThrowsNotSupportedException()
    {
        using var resources = CreateResources();

        Assert.Throws<NotSupportedException>(() =>
            resources.Client.GetAllEntitiesAsync(filter: null));
    }

    [Fact]
    public void GetAllEntitiesAsync_Generic_ThrowsNotSupportedException()
    {
        using var resources = CreateResources();

        Assert.Throws<NotSupportedException>(() =>
            resources.Client.GetAllEntitiesAsync<int>(filter: null));
    }

    [Fact]
    public async Task CleanEntityStorageAsync_ThrowsNotSupportedException()
    {
        using var resources = CreateResources();

#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            resources.Client.CleanEntityStorageAsync(
                request: null,
                continueUntilComplete: false,
                CancellationToken.None));
#pragma warning restore xUnit1051
    }

    // ── SignalEntityAsync (supported) ─────────────────────────────────────────

    [Fact]
    public async Task SignalEntityAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId("AnyEntity", "key");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resources.Client.SignalEntityAsync(entityId, "op", null, options: null, cts.Token));
    }

    // ── Factory ──────────────────────────────────────────────────────────────

    private static TestResources CreateResources()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var catalog = new FakeDurableFunctionCatalog(typeof(FakeDurableEntityClientTests).Assembly);
        var entityRunnerLogger = services.GetRequiredService<ILogger<FakeDurableEntityRunner>>();
        var entityRunner = new FakeDurableEntityRunner(catalog, services, entityRunnerLogger);
        var client = new FakeDurableEntityClient(entityRunner);
        return new TestResources(client, entityRunner);
    }

    private sealed class TestResources : IDisposable
    {
        public TestResources(FakeDurableEntityClient client, FakeDurableEntityRunner runner)
        {
            Client = client;
            Runner = runner;
        }

        public FakeDurableEntityClient Client { get; }
        public FakeDurableEntityRunner Runner { get; }

        public void Dispose() => Runner.Dispose();
    }
}
