using TUnit.Core;

// Assembly-wide concurrent test cap (WorkerTUnitParallelLimit).
[assembly: ParallelLimiter<global::Sample.FunctionApp.Worker.TUnit.Tests.WorkerTUnitParallelLimit>]
