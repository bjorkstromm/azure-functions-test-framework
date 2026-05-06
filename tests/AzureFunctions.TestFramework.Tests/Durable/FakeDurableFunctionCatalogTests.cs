using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="FakeDurableFunctionCatalog"/>, covering the happy-path
/// descriptors and the <see cref="InvalidOperationException"/> thrown for unknown names.
/// The test assembly itself contains the helper functions/entities that the catalog scans.
/// </summary>
public class FakeDurableFunctionCatalogTests
{
    /// <summary>
    /// Catalog built from this assembly — it will find the helper functions defined below.
    /// </summary>
    private static readonly FakeDurableFunctionCatalog Catalog =
        new FakeDurableFunctionCatalog(typeof(FakeDurableFunctionCatalogTests).Assembly);

    // ── GetActivity ───────────────────────────────────────────────────────────

    [Fact]
    public void GetActivity_KnownName_ReturnsDescriptorWithCorrectFunctionName()
    {
        var descriptor = Catalog.GetActivity(CatalogTestActivityFunctionName);

        Assert.Equal(CatalogTestActivityFunctionName, descriptor.FunctionName);
        Assert.NotNull(descriptor.Method);
    }

    [Fact]
    public void GetActivity_UnknownName_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Catalog.GetActivity("NonExistentCatalogActivity"));

        Assert.Contains("NonExistentCatalogActivity", ex.Message);
    }

    [Fact]
    public void GetActivity_CaseInsensitive_FindsFunction()
    {
        // Function names are matched case-insensitively
        var descriptor = Catalog.GetActivity(CatalogTestActivityFunctionName.ToUpperInvariant());
        Assert.Equal(CatalogTestActivityFunctionName, descriptor.FunctionName);
    }

    // ── GetOrchestrator ───────────────────────────────────────────────────────

    [Fact]
    public void GetOrchestrator_KnownName_ReturnsDescriptorWithCorrectFunctionName()
    {
        var descriptor = Catalog.GetOrchestrator(CatalogTestOrchestratorFunctionName);

        Assert.Equal(CatalogTestOrchestratorFunctionName, descriptor.FunctionName);
        Assert.NotNull(descriptor.Method);
    }

    [Fact]
    public void GetOrchestrator_UnknownName_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Catalog.GetOrchestrator("NonExistentCatalogOrchestrator"));

        Assert.Contains("NonExistentCatalogOrchestrator", ex.Message);
    }

    [Fact]
    public void GetOrchestrator_CaseInsensitive_FindsFunction()
    {
        var descriptor = Catalog.GetOrchestrator(CatalogTestOrchestratorFunctionName.ToLowerInvariant());
        Assert.Equal(CatalogTestOrchestratorFunctionName, descriptor.FunctionName);
    }

    // ── GetEntityType ─────────────────────────────────────────────────────────

    [Fact]
    public void GetEntityType_KnownName_ReturnsCorrectType()
    {
        var type = Catalog.GetEntityType(CatalogTestEntityFunctionName);

        Assert.Equal(typeof(CatalogTestEntity), type);
    }

    [Fact]
    public void GetEntityType_UnknownName_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Catalog.GetEntityType("NonExistentCatalogEntity"));

        Assert.Contains("NonExistentCatalogEntity", ex.Message);
    }

    [Fact]
    public void GetEntityType_CaseInsensitive_FindsEntity()
    {
        var type = Catalog.GetEntityType(CatalogTestEntityFunctionName.ToUpperInvariant());
        Assert.Equal(typeof(CatalogTestEntity), type);
    }

    // ── Function name constants ───────────────────────────────────────────────

    private const string CatalogTestActivityFunctionName = "CatalogTestActivity";
    private const string CatalogTestOrchestratorFunctionName = "CatalogTestOrchestrator";
    private const string CatalogTestEntityFunctionName = "CatalogTestEntity";

    // ── Helper functions in this assembly for catalog scanning ────────────────

    [Function(CatalogTestActivityFunctionName)]
    public static string CatalogTestActivityFn([ActivityTrigger] string input) => input;

    [Function(CatalogTestOrchestratorFunctionName)]
    public static Task CatalogTestOrchestratorFn(
        [OrchestrationTrigger] TaskOrchestrationContext ctx) => Task.CompletedTask;

    /// <summary>
    /// A minimal <see cref="ITaskEntity"/> implementation whose class name matches the
    /// function name so the catalog can resolve it via the entity-implementation lookup.
    /// </summary>
    public sealed class CatalogTestEntity : TaskEntity<int>
    {
        /// <summary>Returns the current state value.</summary>
        public int Get() => State;

        [Function(CatalogTestEntityFunctionName)]
        public Task Run([EntityTrigger] TaskEntityDispatcher dispatcher)
            => dispatcher.DispatchAsync<CatalogTestEntity>();
    }
}
