using System.Runtime.CompilerServices;
using VerifyTests;

namespace Sample.FunctionApp.Durable.Tests;

/// <summary>
/// Represents this type.
/// </summary>
public static class VerifyConfig
{
    /// <summary>
    /// Executes this operation.
    /// </summary>
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.ScrubInlineGuids();
        Verifier.UseProjectRelativeDirectory("Snapshots");
    }
}
