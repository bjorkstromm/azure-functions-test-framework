using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

public class FakeTaskEntityContextTests
{
    [Fact]
    public void ScheduleNewOrchestration_WithoutDelegate_Throws()
    {
        using var resources = CreateResources();
        var sut = new FakeTaskEntityContext(new EntityInstanceId("Entity", "id"), resources.EntityRunner, resources.Logger);

        Assert.Throws<NotSupportedException>(() => sut.ScheduleNewOrchestration(new TaskName("orch"), null, null));
    }

    [Fact]
    public void ScheduleNewOrchestration_WithDelegate_ReturnsInstanceId()
    {
        using var resources = CreateResources();
        var sut = new FakeTaskEntityContext(
            new EntityInstanceId("Entity", "id"),
            resources.EntityRunner,
            resources.Logger,
            (name, _, _) => $"{name}-instance");

        var result = sut.ScheduleNewOrchestration(new TaskName("orch"), null, null);

        Assert.Equal("orch-instance", result);
    }

    [Fact]
    public async Task SignalEntity_FireAndForget_ExecutesOperation()
    {
        using var resources = CreateResources();
        var entityId = new EntityInstanceId(nameof(MutableTestEntity), "signal-1");
        var sut = new FakeTaskEntityContext(entityId, resources.EntityRunner, resources.Logger);

        sut.SignalEntity(entityId, "add", 4, options: null);

        var attempts = 0;
        object? value = null;
        while (attempts++ < 20)
        {
            value = await resources.EntityRunner.CallEntityAsync(entityId, "get", null, TestContext.Current.CancellationToken);
            if (value is int i && i == 4)
            {
                break;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.Equal(4, value);
    }

    private static TestResources CreateResources()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var catalog = new FakeDurableFunctionCatalog(typeof(FakeDurableEntityClientTests).Assembly);
        var logger = services.GetRequiredService<ILogger<FakeDurableEntityRunner>>();
        var runner = new FakeDurableEntityRunner(catalog, services, logger);
        return new TestResources(runner, logger);
    }

    private sealed class TestResources : IDisposable
    {
        public TestResources(FakeDurableEntityRunner entityRunner, ILogger<FakeDurableEntityRunner> logger)
        {
            EntityRunner = entityRunner;
            Logger = logger;
        }

        public FakeDurableEntityRunner EntityRunner { get; }
        public ILogger Logger { get; }

        public void Dispose() => EntityRunner.Dispose();
    }
}
