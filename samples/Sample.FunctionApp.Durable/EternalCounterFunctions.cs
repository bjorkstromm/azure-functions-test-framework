using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sample.FunctionApp.Durable;

/// <summary>
/// Demonstrates an eternal-orchestrator singleton pattern:
/// a counter that runs forever, waiting for an external "increment" event each iteration,
/// then calling <see cref="TaskOrchestrationContext.ContinueAsNew"/> to restart with the
/// updated count.  The orchestration can be suspended, resumed, or terminated via an HTTP
/// control trigger, which exercises the full instance lifecycle supported by
/// <c>FakeDurableTaskClient</c>.
/// </summary>
public class EternalCounterFunctions
{
    private const string SingletonInstanceId = "eternal-counter-singleton";

    // ── HTTP triggers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Starts (or re-starts after termination/completion) the singleton eternal-counter
    /// orchestration.  Returns <c>409 Conflict</c> when the instance is already running
    /// or pending.
    /// </summary>
    [Function(nameof(StartEternalCounter))]
    public async Task<HttpResponseData> StartEternalCounter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "eternal/start")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var existing = await durableClient.GetInstancesAsync(
            SingletonInstanceId, false, cancellationToken);

        if (existing?.RuntimeStatus is
            OrchestrationRuntimeStatus.Running or OrchestrationRuntimeStatus.Pending)
        {
            var conflict = request.CreateResponse(HttpStatusCode.Conflict);
            await conflict.WriteStringAsync(
                $"Orchestration '{SingletonInstanceId}' is already {existing.RuntimeStatus}.",
                cancellationToken);
            return conflict;
        }

        await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(RunEternalCounter),
            input: 0,
            options: new StartOrchestrationOptions { InstanceId = SingletonInstanceId },
            cancellation: cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteStringAsync(SingletonInstanceId, cancellationToken);
        return response;
    }

    /// <summary>
    /// Returns the current status of the singleton eternal-counter orchestration.
    /// </summary>
    [Function(nameof(GetEternalCounterStatus))]
    public async Task<HttpResponseData> GetEternalCounterStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "eternal/{instanceId}/status")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var metadata = await durableClient.GetInstancesAsync(
            instanceId, getInputsAndOutputs: true, cancellationToken);

        if (metadata is null)
        {
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync($"Instance '{instanceId}' not found.", cancellationToken);
            return notFound;
        }

        var status = new EternalCounterStatusDocument(
            metadata.InstanceId,
            metadata.RuntimeStatus.ToString(),
            metadata.ReadCustomStatusAs<EternalCounterStatus>()?.Count ?? 0,
            metadata.LastUpdatedAt);

        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(status), cancellationToken);
        return response;
    }

    /// <summary>
    /// Raises an "increment" external event on the orchestration, causing the counter to
    /// advance by one on the next iteration.
    /// </summary>
    [Function(nameof(IncrementEternalCounter))]
    public async Task<HttpResponseData> IncrementEternalCounter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "eternal/{instanceId}/increment")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        string instanceId,
        CancellationToken cancellationToken)
    {
        await durableClient.RaiseEventAsync(instanceId, "increment", null, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.Accepted);
        return response;
    }

    /// <summary>
    /// Controls the lifecycle of a running orchestration: <c>suspend</c>, <c>resume</c>,
    /// <c>terminate</c> (with optional <c>"force": true</c> to skip the running-status guard),
    /// or <c>purge</c>.
    /// </summary>
    [Function(nameof(ControlEternalCounter))]
    public async Task<HttpResponseData> ControlEternalCounter(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "eternal/{instanceId}/control")]
        HttpRequestData request,
        [DurableClient] DurableTaskClient durableClient,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var body = await request.ReadAsStringAsync();
        EternalCounterControl? control = null;
        try
        {
            control = JsonSerializer.Deserialize<EternalCounterControl>(body ?? "{}");
        }
        catch (JsonException)
        {
            // fall through to bad-request below
        }

        if (control?.Action is null)
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync(
                "Request body must contain a JSON object with an \"action\" field.", cancellationToken);
            return badRequest;
        }

        switch (control.Action.ToLowerInvariant())
        {
            case "suspend":
                await durableClient.SuspendInstanceAsync(instanceId, control.Reason, cancellationToken);
                break;

            case "resume":
                await durableClient.ResumeInstanceAsync(instanceId, control.Reason, cancellationToken);
                break;

            case "terminate":
            {
                if (!control.Force)
                {
                    var existing = await durableClient.GetInstancesAsync(
                        instanceId, false, cancellationToken);
                    if (existing?.RuntimeStatus is not OrchestrationRuntimeStatus.Running)
                    {
                        var conflict = request.CreateResponse(HttpStatusCode.Conflict);
                        await conflict.WriteStringAsync(
                            $"Instance '{instanceId}' is not running (status: {existing?.RuntimeStatus}). " +
                            "Use \"force\": true to force-terminate.",
                            cancellationToken);
                        return conflict;
                    }
                }

                await durableClient.TerminateInstanceAsync(
                    instanceId,
                    new TerminateInstanceOptions { Output = control.Reason },
                    cancellationToken);
                break;
            }

            case "purge":
                await durableClient.PurgeInstanceAsync(instanceId, null, cancellationToken);
                break;

            default:
            {
                var bad = request.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync(
                    $"Unknown action '{control.Action}'. Supported: suspend, resume, terminate, purge.",
                    cancellationToken);
                return bad;
            }
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync(
            $"Action '{control.Action}' applied to instance '{instanceId}'.", cancellationToken);
        return response;
    }

    // ── Orchestrator ──────────────────────────────────────────────────────────

    /// <summary>
    /// Eternal orchestrator: waits for an "increment" external event, advances the counter
    /// by one, then calls <see cref="TaskOrchestrationContext.ContinueAsNew"/> to loop forever.
    /// </summary>
    [Function(nameof(RunEternalCounter))]
    public static async Task RunEternalCounter(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var count = context.GetInput<int>();
        context.SetCustomStatus(new EternalCounterStatus(count));

        await context.WaitForExternalEvent<object?>("increment");

        context.ContinueAsNew(count + 1);
    }
}

/// <summary>Custom status written by the eternal counter on each iteration.</summary>
public sealed record EternalCounterStatus(
    [property: JsonPropertyName("count")] int Count);

/// <summary>Status document returned by <c>GetEternalCounterStatus</c>.</summary>
public sealed record EternalCounterStatusDocument(
    [property: JsonPropertyName("instanceId")] string InstanceId,
    [property: JsonPropertyName("runtimeStatus")] string RuntimeStatus,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("lastUpdated")] DateTimeOffset LastUpdated);

/// <summary>Request body accepted by <c>ControlEternalCounter</c>.</summary>
public sealed class EternalCounterControl
{
    /// <summary>Action to perform: suspend, resume, terminate, purge.</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>Optional human-readable reason, forwarded to the durable backend.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// When <c>true</c> on a <c>terminate</c> action, skips the running-status guard and
    /// force-terminates regardless of the current state.
    /// </summary>
    [JsonPropertyName("force")]
    public bool Force { get; set; }
}
