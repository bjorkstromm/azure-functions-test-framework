using TUnit.Core.Interfaces;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Shared parallel execution cap for this test assembly. Limits how many tests run at once
/// to reduce contention when spinning up Azure Functions test hosts.
/// </summary>
public sealed class WorkerTUnitParallelLimit : IParallelLimit
{
    /// <summary>
    /// Maximum number of tests from this assembly that may run at the same time.
    /// </summary>
    public int Limit => 4;
}
