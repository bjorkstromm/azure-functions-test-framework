using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using System.Net;

namespace TestProject;

public class DurableFunction
{
    [Function("StartOrchestration")]
    public async Task<string> StartOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "durable/start/{name}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string name,
        CancellationToken ct)
    {
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunOrchestration), name, ct);
        var metadata = await client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true, ct);
        return metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
            ? metadata.ReadOutputAs<string>() ?? string.Empty
            : metadata.FailureDetails?.ErrorMessage ?? "Orchestration did not complete";
    }

    [Function(nameof(RunOrchestration))]
    public async Task<string> RunOrchestration([OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var name = ctx.GetInput<string>() ?? "World";
        return await ctx.CallActivityAsync<string>(nameof(SayHello), name);
    }

    [Function(nameof(SayHello))]
    public string SayHello([ActivityTrigger] string name) => $"Hello, {name}!";
}

public sealed class DurableCounterEntity : TaskEntity<int>
{
    public void Add(int amount) => State += amount;
    public void Reset() => State = 0;
    public int Get() => State;

    [Function(nameof(DurableCounterEntity))]
    public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher) => dispatcher.DispatchAsync<DurableCounterEntity>();
}
