using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Sample.FunctionApp.Durable;

public class DurableGreetingFunctions
{
    [Function(nameof(StartGreetingOrchestration))]
    public async Task<string> StartGreetingOrchestration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "durable/hello/{name}")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        string name,
        CancellationToken cancellationToken)
    {
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunGreetingOrchestration),
            name,
            cancellationToken);

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true,
            cancellationToken);

        return metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
            ? metadata.ReadOutputAs<string>() ?? string.Empty
            : metadata.FailureDetails?.ErrorMessage ?? "The fake durable orchestration did not complete successfully.";
    }

    [Function(nameof(RunGreetingOrchestration))]
    public static async Task<string> RunGreetingOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var name = context.GetInput<string>() ?? string.Empty;
        return await context.CallActivityAsync<string>(nameof(CreateGreeting), name);
    }

    [Function(nameof(CreateGreeting))]
    public static string CreateGreeting([ActivityTrigger] string name)
    {
        return $"Hello, {name}!";
    }
}
