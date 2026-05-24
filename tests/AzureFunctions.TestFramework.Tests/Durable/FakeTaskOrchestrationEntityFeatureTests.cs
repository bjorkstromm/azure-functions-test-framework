using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Represents this type.
/// </summary>
public class FakeTaskOrchestrationEntityFeatureTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task EntityFeature_DelegatesSignalAndCallOperations()
    {
        using var resources = CreateResources();
        var feature = new FakeTaskOrchestrationEntityFeature(resources.EntityRunner);
        var entityId = new EntityInstanceId(nameof(MutableTestEntity), "feature-key");

        await feature.SignalEntityAsync(entityId, "add", 5, options: null);
        var value = await feature.CallEntityAsync<int>(entityId, "get", input: null, options: null);
        await feature.CallEntityAsync(entityId, "add", 2, options: null);
        var value2 = await feature.CallEntityAsync<int>(entityId, "get", input: null, options: null);

        Assert.Equal(5, value);
        Assert.Equal(7, value2);
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task EntityFeature_LockAndCriticalSection_AreNoOp()
    {
        using var resources = CreateResources();
        var feature = new FakeTaskOrchestrationEntityFeature(resources.EntityRunner);

        var lockHandle = await feature.LockEntitiesAsync([new EntityInstanceId("Any", "1")]);
        var inCritical = feature.InCriticalSection(out var entityIds);
        await lockHandle.DisposeAsync();

        Assert.False(inCritical);
        Assert.Empty(entityIds);
    }

    private static TestResources CreateResources()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var catalog = new FakeDurableFunctionCatalog(typeof(FakeDurableEntityClientTests).Assembly);
        var logger = services.GetRequiredService<ILogger<FakeDurableEntityRunner>>();
        var entityRunner = new FakeDurableEntityRunner(catalog, services, logger);
        return new TestResources(entityRunner);
    }

    private sealed class TestResources : IDisposable
    {
        public TestResources(FakeDurableEntityRunner entityRunner) => EntityRunner = entityRunner;

        public FakeDurableEntityRunner EntityRunner { get; }

        public void Dispose() => EntityRunner.Dispose();
    }
}
