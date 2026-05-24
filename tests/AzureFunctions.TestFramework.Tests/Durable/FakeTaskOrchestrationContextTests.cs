using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Represents this type.
/// </summary>
public class FakeTaskOrchestrationContextTests
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task Context_ActivitySubOrchestrationAndState_APIs_Work()
    {
        using var resources = CreateResources();
        var raisedEvents = new List<(string InstanceId, string EventName, object? Payload)>();
        object? customStatus = null;

        var sut = new FakeTaskOrchestrationContext(
            orchestrationName: "MyOrchestrator",
            instanceId: "instance-1",
            input: 5,
            serviceProvider: resources.Services,
            activityDispatcher: (_, input, _) => Task.FromResult<object?>((int)input! + 1),
            subOrchestrationDispatcher: (_, _, _) => Task.FromResult<object?>("sub-result"),
            externalEventDispatcher: (_, _, _) => Task.FromResult<object?>("external"),
            eventRaiser: (instanceId, eventName, payload) =>
            {
                raisedEvents.Add((instanceId, eventName, payload));
                return Task.CompletedTask;
            },
            customStatusSink: status => customStatus = status,
            entityRunner: resources.EntityRunner);

        var activity = await sut.CallActivityAsync<int>(new TaskName("A"), 5);
        var sub = await sut.CallSubOrchestratorAsync<string>(new TaskName("B"), input: null);
        var external = await sut.WaitForExternalEvent<string>("evt", TestContext.Current.CancellationToken);
        var timer = sut.CreateTimer(DateTime.UtcNow.AddMilliseconds(1), TestContext.Current.CancellationToken);
        sut.ContinueAsNew(new { value = 123 });
        sut.SetCustomStatus("done");
        sut.SendEvent("target-instance", "my-event", new { x = 1 });
        var input = sut.GetInput<int>();
        var id = sut.NewGuid();
        await timer;

        Assert.Equal(6, activity);
        Assert.Equal("sub-result", sub);
        Assert.Equal("external", external);
        Assert.Equal(5, input);
        Assert.NotEqual(Guid.Empty, id);
        Assert.True(sut.IsContinueAsNew);
        Assert.NotNull(sut.ContinueAsNewInput);
        Assert.Equal("done", sut.CustomStatus);
        Assert.Equal("done", customStatus);
        Assert.Single(raisedEvents);
        Assert.Equal(("target-instance", "my-event"), (raisedEvents[0].InstanceId, raisedEvents[0].EventName));
    }

    /// <summary>
    /// Executes this operation.
    /// </summary>
    [Fact]
    public async Task WaitForExternalEvent_WithCanceledToken_Throws()
    {
        using var resources = CreateResources();
        var sut = new FakeTaskOrchestrationContext(
            orchestrationName: "MyOrchestrator",
            instanceId: "instance-2",
            input: null,
            serviceProvider: resources.Services,
            activityDispatcher: (_, _, _) => Task.FromResult<object?>(null),
            subOrchestrationDispatcher: (_, _, _) => Task.FromResult<object?>(null),
            externalEventDispatcher: (_, _, ct) => Task.Delay(TimeSpan.FromSeconds(5), ct).ContinueWith(_ => (object?)null, ct),
            entityRunner: resources.EntityRunner);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.WaitForExternalEvent<string>("evt", cts.Token));
    }

    private static TestResources CreateResources()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var catalog = new FakeDurableFunctionCatalog(typeof(FakeDurableEntityClientTests).Assembly);
        var logger = services.GetRequiredService<ILogger<FakeDurableEntityRunner>>();
        var entityRunner = new FakeDurableEntityRunner(catalog, services, logger);
        return new TestResources(services, entityRunner);
    }

    private sealed class TestResources : IDisposable
    {
        public TestResources(ServiceProvider services, FakeDurableEntityRunner entityRunner)
        {
            Services = services;
            EntityRunner = entityRunner;
        }

        public ServiceProvider Services { get; }
        public FakeDurableEntityRunner EntityRunner { get; }

        public void Dispose() => EntityRunner.Dispose();
    }
}
