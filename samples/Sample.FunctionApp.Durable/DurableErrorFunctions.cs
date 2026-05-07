using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Sample.FunctionApp.Durable;

/// <summary>
/// Helper durable functions used by error-handling and edge-case tests.
/// These functions deliberately throw exceptions or exercise special
/// <see cref="TaskOrchestrationContext"/> APIs to verify that the fake
/// durable runner handles such scenarios correctly.
/// </summary>
public static class DurableErrorFunctions
{
    /// <summary>Activity that always throws to simulate an activity failure.</summary>
    [Function(nameof(ThrowingActivity))]
    public static string ThrowingActivity([ActivityTrigger] string name)
        => throw new InvalidOperationException($"Activity deliberately failed for: {name}");

    /// <summary>Orchestrator that always throws to simulate an orchestration failure.</summary>
    [Function(nameof(ThrowingOrchestration))]
    public static Task ThrowingOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
        => throw new InvalidOperationException("Orchestration deliberately failed.");

    /// <summary>Orchestrator that calls a throwing activity, propagating the failure.</summary>
    [Function(nameof(ActivityThrowingOrchestration))]
    public static Task<string> ActivityThrowingOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
        => context.CallActivityAsync<string>(nameof(ThrowingActivity), "test");

    /// <summary>
    /// Orchestrator that calls <see cref="TaskOrchestrationContext.CreateTimer"/> (a no-op in
    /// the fake runner) then returns a sentinel value, verifying that <c>CreateTimer</c> does
    /// not block or throw.
    /// </summary>
    [Function(nameof(TimerOrchestration))]
    public static async Task<string> TimerOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(60), CancellationToken.None);
        return "timer-completed";
    }

    /// <summary>
    /// Orchestrator that calls <see cref="TaskOrchestrationContext.SendEvent"/>, which is not
    /// supported by the fake runner and throws <see cref="NotSupportedException"/>, causing the
    /// orchestration to transition to the <c>Failed</c> status.
    /// </summary>
    [Function(nameof(SendEventOrchestration))]
    public static void SendEventOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
        => context.SendEvent("some-target-instance", "test-event", "payload");
}
