using System.Runtime.CompilerServices;
using VerifyTests;

namespace Sample.FunctionApp.Worker.TUnit.Tests;

/// <summary>
/// Initializes Verify snapshot settings for this test assembly (scrubbing and snapshot directory).
/// </summary>
public static class VerifyConfig
{
    /// <summary>
    /// Registers Verify defaults when the assembly loads.
    /// </summary>
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.ScrubInlineGuids();
        Verifier.UseProjectRelativeDirectory("Snapshots");
    }
}
