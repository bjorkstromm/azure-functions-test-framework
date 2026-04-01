using System.Runtime.CompilerServices;
using VerifyTests;

namespace Sample.FunctionApp.CustomRoutePrefix.WAF.Tests;

public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifierSettings.ScrubInlineGuids();
        Verifier.UseProjectRelativeDirectory("Snapshots");
    }
}
