using AzureFunctions.TestFramework.Core;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="FunctionsTestHostBuilder"/> and its configuration methods.
/// </summary>
public class FunctionsTestHostBuilderTests
{
    // ── Build without assembly ────────────────────────────────────────────────

    [Fact]
    public void Build_WithoutFunctionsAssembly_ThrowsInvalidOperationException()
    {
        var builder = new FunctionsTestHostBuilder();
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("WithFunctionsAssembly", ex.Message);
    }

    // ── CreateBuilder static helpers ──────────────────────────────────────────

    [Fact]
    public void CreateBuilder_ReturnsNewBuilder()
    {
        var builder = FunctionsTestHost.CreateBuilder();
        Assert.NotNull(builder);
        Assert.IsType<FunctionsTestHostBuilder>(builder);
    }

    [Fact]
    public void CreateBuilderGeneric_SetsAssembly()
    {
        // Should not throw - uses this test class's assembly
        var builder = FunctionsTestHost.CreateBuilder<FunctionsTestHostBuilderTests>();
        Assert.NotNull(builder);
    }

    // ── Fluent builder methods return the same builder instance ───────────────

    [Fact]
    public void ConfigureServices_ReturnsSameBuilder()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        var result = builder.ConfigureServices(_ => { });
        Assert.Same(builder, result);
    }

    [Fact]
    public void ConfigureSetting_ReturnsSameBuilder()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        var result = builder.ConfigureSetting("key", "value");
        Assert.Same(builder, result);
    }

    [Fact]
    public void ConfigureEnvironmentVariable_ReturnsSameBuilder()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        var result = builder.ConfigureEnvironmentVariable("SOME_VAR", "some-value");
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithInvocationTimeout_ReturnsSameBuilder()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        var result = builder.WithInvocationTimeout(TimeSpan.FromSeconds(30));
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithSyntheticBindingProvider_ReturnsSameBuilder()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        var provider = new FakeProvider();
        var result = builder.WithSyntheticBindingProvider(provider);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithSyntheticBindingProvider_NullProvider_Throws()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.WithSyntheticBindingProvider(null!));
    }

    [Fact]
    public void WithHostBuilderFactory_ReturnsSameBuilder()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        var result = builder.WithHostBuilderFactory(_ => null!);
        Assert.Same(builder, result);
    }

    [Fact]
    public void WithHostApplicationBuilderFactory_ReturnsSameBuilder()
    {
        IFunctionsTestHostBuilder builder = new FunctionsTestHostBuilder();
        var result = builder.WithHostApplicationBuilderFactory(_ => null!);
        Assert.Same(builder, result);
    }

    // ── ReadRoutePrefixFromHostJson via reflection ─────────────────────────────

    [Fact]
    public void ReadRoutePrefixFromHostJson_NoHostJson_ReturnsApi()
    {
        // This assembly has no host.json in its output directory
        var prefix = InvokeReadRoutePrefixFromHostJson(typeof(FunctionsTestHostBuilderTests).Assembly);
        Assert.Equal("api", prefix);
    }

    [Fact]
    public void TryReadRoutePrefixFromFile_NonExistentFile_ReturnsNull()
    {
        var result = InvokeTryReadRoutePrefixFromFile("/tmp/does-not-exist-xyz.json");
        Assert.Null(result);
    }

    [Fact]
    public void TryReadRoutePrefixFromFile_ValidHostJsonWithRoutePrefix_ReturnsPrefix()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"extensions":{"http":{"routePrefix":"v1"}}}""");
            var result = InvokeTryReadRoutePrefixFromFile(path);
            Assert.Equal("v1", result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryReadRoutePrefixFromFile_ValidHostJsonWithoutRoutePrefix_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"extensions":{"http":{}}}""");
            var result = InvokeTryReadRoutePrefixFromFile(path);
            Assert.Null(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryReadRoutePrefixFromFile_InvalidJson_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "NOT-VALID-JSON");
            var result = InvokeTryReadRoutePrefixFromFile(path);
            Assert.Null(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryReadRoutePrefixFromFile_EmptyJsonObject_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            var result = InvokeTryReadRoutePrefixFromFile(path);
            Assert.Null(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string InvokeReadRoutePrefixFromHostJson(System.Reflection.Assembly assembly)
    {
        var method = typeof(FunctionsTestHostBuilder)
            .GetMethod("ReadRoutePrefixFromHostJson",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [assembly])!;
    }

    private static string? InvokeTryReadRoutePrefixFromFile(string path)
    {
        var method = typeof(FunctionsTestHostBuilder)
            .GetMethod("TryReadRoutePrefixFromFile",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string?)method.Invoke(null, [path]);
    }

    private sealed class FakeProvider : ISyntheticBindingProvider
    {
        public string BindingType => "fake";
        public FunctionBindingData? CreateSyntheticParameter(string parameterName, System.Text.Json.JsonElement bindingConfig) => null;
    }
}
