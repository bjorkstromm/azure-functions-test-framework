using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    [Function(nameof(StartGreetingViaSubOrchestrator))]
    public async Task<string> StartGreetingViaSubOrchestrator(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "durable/hello/sub/{name}")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        string name,
        CancellationToken cancellationToken)
    {
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunGreetingParentOrchestration),
            name,
            cancellationToken);

        var metadata = await durableClient.WaitForInstanceCompletionAsync(
            instanceId,
            getInputsAndOutputs: true,
            cancellationToken);

        return metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed
            ? metadata.ReadOutputAs<string>() ?? string.Empty
            : metadata.FailureDetails?.ErrorMessage ?? "The fake durable sub-orchestrator did not complete successfully.";
    }

    [Function(nameof(StartGreetingWithManagementPayload))]
    public async Task<string> StartGreetingWithManagementPayload(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "durable/manage/{name}")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        string name,
        CancellationToken cancellationToken)
    {
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunGreetingStatusOrchestration),
            name,
            cancellationToken);

        return JsonSerializer.Serialize(CreateManagementPayload(instanceId));
    }

    [Function(nameof(GetGreetingStatusDocument))]
    public async Task<string> GetGreetingStatusDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "durable/manage/status/{instanceId}")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var metadata = await durableClient.GetInstancesAsync(instanceId, getInputsAndOutputs: true, cancellationToken);
        if (metadata is null)
        {
            throw new InvalidOperationException($"The fake durable orchestration instance '{instanceId}' does not exist.");
        }

        return JsonSerializer.Serialize(CreateStatusDocument(metadata));
    }

    [Function(nameof(RunGreetingOrchestration))]
    public static async Task<string> RunGreetingOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var name = context.GetInput<string>() ?? string.Empty;
        return await context.CallActivityAsync<string>(nameof(CreateGreeting), name);
    }

    [Function(nameof(RunGreetingParentOrchestration))]
    public static async Task<string> RunGreetingParentOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var name = context.GetInput<string>() ?? string.Empty;
        var greeting = await context.CallSubOrchestratorAsync<string>(nameof(RunGreetingChildOrchestration), name);
        return $"{greeting} (from parent)";
    }

    [Function(nameof(RunGreetingChildOrchestration))]
    public static async Task<string> RunGreetingChildOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var name = context.GetInput<string>() ?? string.Empty;
        return await context.CallActivityAsync<string>(nameof(CreateGreeting), name);
    }

    [Function(nameof(RunGreetingStatusOrchestration))]
    public static async Task<string> RunGreetingStatusOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var name = context.GetInput<string>() ?? string.Empty;
        context.SetCustomStatus(new GreetingProgressStatus("running", name, null));

        var greeting = await context.CallActivityAsync<string>(nameof(CreateGreeting), name);

        context.SetCustomStatus(new GreetingProgressStatus("completed", name, greeting));
        return greeting;
    }

    [Function(nameof(CreateGreeting))]
    public static string CreateGreeting([ActivityTrigger] string name)
    {
        return $"Hello, {name}!";
    }

    private static DurableHttpManagementPayload CreateManagementPayload(string instanceId)
    {
        return new DurableHttpManagementPayload
        {
            Id = instanceId,
            StatusQueryGetUri = $"/api/durable/manage/status/{Uri.EscapeDataString(instanceId)}"
        };
    }

    private static DurableOrchestrationStatus CreateStatusDocument(OrchestrationMetadata metadata)
    {
        return new DurableOrchestrationStatus
        {
            Name = metadata.Name,
            InstanceId = metadata.InstanceId,
            RuntimeStatus = metadata.RuntimeStatus.ToString(),
            Input = ParseJsonElement(metadata.SerializedInput),
            Output = ParseJsonElement(metadata.SerializedOutput),
            CustomStatus = ParseJsonElement(metadata.SerializedCustomStatus),
            CreatedTime = metadata.CreatedAt,
            LastUpdatedTime = metadata.LastUpdatedAt
        };
    }

    private static JsonElement ParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

public sealed record GreetingProgressStatus(
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("message")] string? Message);
