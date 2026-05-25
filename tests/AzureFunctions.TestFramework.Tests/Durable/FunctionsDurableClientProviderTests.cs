using AzureFunctions.TestFramework.Durable;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

public class FunctionsDurableClientProviderTests
{
    [Fact]
    public void Provider_ReturnsSameClient_FromPropertyAndMethod()
    {
        using var resources = CreateResources();
        var provider = new FunctionsDurableClientProvider(resources.Client);

        Assert.Same(resources.Client, provider.Client);
        Assert.Same(resources.Client, provider.GetClient());
    }

    private static TestResources CreateResources()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var catalog = new FakeDurableFunctionCatalog(typeof(FakeDurableEntityClientTests).Assembly);
        var externalEventHub = new FakeDurableExternalEventHub();
        var entityRunnerLogger = services.GetRequiredService<ILogger<FakeDurableEntityRunner>>();
        var entityRunner = new FakeDurableEntityRunner(catalog, services, entityRunnerLogger);
        var runnerLogger = services.GetRequiredService<ILogger<FakeDurableOrchestrationRunner>>();
        var runner = new FakeDurableOrchestrationRunner(catalog, externalEventHub, services, runnerLogger, entityRunner);
        var entityClient = new FakeDurableEntityClient(entityRunner);
        var clientLogger = services.GetRequiredService<ILogger<FakeDurableTaskClient>>();
        var client = new FakeDurableTaskClient(runner, externalEventHub, entityClient, clientLogger);
        return new TestResources(client, entityRunner);
    }

    private sealed class TestResources : IDisposable
    {
        public TestResources(DurableTaskClient client, FakeDurableEntityRunner entityRunner)
        {
            Client = client;
            EntityRunner = entityRunner;
        }

        public DurableTaskClient Client { get; }
        private FakeDurableEntityRunner EntityRunner { get; }

        public void Dispose() => EntityRunner.Dispose();
    }
}
