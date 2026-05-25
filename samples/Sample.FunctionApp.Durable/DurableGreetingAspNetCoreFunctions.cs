using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;

namespace Sample.FunctionApp.Durable;

/// <summary>
/// Durable starter functions using ASP.NET Core native types (<see cref="HttpRequest"/>,
/// <see cref="IActionResult"/>) for use with <c>ConfigureFunctionsWebApplication()</c>.
/// </summary>
public class DurableGreetingAspNetCoreFunctions
{
    [Function(nameof(StartGreetingOrchestrationAspNetCore))]
    public async Task<IActionResult> StartGreetingOrchestrationAspNetCore(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "aspnetcore/durable/hello/{name}")]
        HttpRequest request,
        [DurableClient] DurableTaskClient durableClient,
        string name,
        CancellationToken cancellationToken)
    {
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(DurableGreetingFunctions.RunGreetingOrchestration),
            name,
            cancellationToken);

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true,
            cancellationToken);

        return metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
            ? new OkObjectResult(metadata.ReadOutputAs<string>())
            : new ObjectResult(metadata.FailureDetails?.ErrorMessage ?? "Orchestration did not complete") { StatusCode = 500 };
    }
}
