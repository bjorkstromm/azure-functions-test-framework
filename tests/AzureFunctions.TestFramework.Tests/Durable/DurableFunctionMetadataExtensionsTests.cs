using AzureFunctions.TestFramework.Durable;
using Microsoft.Azure.Functions.Worker.Core.FunctionMetadata;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Durable;

/// <summary>
/// Unit tests for <see cref="DurableFunctionMetadataExtensions"/>.
/// </summary>
public class DurableFunctionMetadataExtensionsTests
{
    [Fact]
    public void HasBindingType_MatchingType_ReturnsTrue()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"durableClient","direction":"In","name":"client"}"""
        ]);
        Assert.True(metadata.HasBindingType("durableClient"));
    }

    [Fact]
    public void HasBindingType_MatchingType_CaseInsensitive_ReturnsTrue()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"DURABLECLIENT","direction":"In","name":"client"}"""
        ]);
        Assert.True(metadata.HasBindingType("durableClient"));
    }

    [Fact]
    public void HasBindingType_NoMatchingType_ReturnsFalse()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"httpTrigger","direction":"In","name":"req"}"""
        ]);
        Assert.False(metadata.HasBindingType("durableClient"));
    }

    [Fact]
    public void HasBindingType_EmptyRawBindings_ReturnsFalse()
    {
        var metadata = CreateMetadata([]);
        Assert.False(metadata.HasBindingType("durableClient"));
    }

    [Fact]
    public void HasBindingType_NullRawBindings_ReturnsFalse()
    {
        var metadata = CreateMetadata(null);
        Assert.False(metadata.HasBindingType("durableClient"));
    }

    [Fact]
    public void HasBindingType_BindingWithoutTypeProperty_Skipped()
    {
        var metadata = CreateMetadata(
        [
            """{"direction":"In","name":"param"}"""
        ]);
        Assert.False(metadata.HasBindingType("durableClient"));
    }

    [Fact]
    public void HasBindingType_MultipleBindings_FindsCorrectOne()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"httpTrigger","direction":"In","name":"req"}""",
            """{"type":"durableClient","direction":"In","name":"client"}""",
            """{"type":"http","direction":"Out","name":"$return"}"""
        ]);
        Assert.True(metadata.HasBindingType("durableClient"));
        Assert.True(metadata.HasBindingType("httpTrigger"));
        Assert.False(metadata.HasBindingType("timerTrigger"));
    }

    [Fact]
    public void HasBindingType_NullMetadata_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IFunctionMetadata)null!).HasBindingType("durableClient"));
    }

    [Fact]
    public void HasBindingType_EmptyBindingType_Throws()
    {
        var metadata = CreateMetadata([]);
        Assert.Throws<ArgumentException>(() => metadata.HasBindingType(""));
    }

    [Fact]
    public void GetDurableTriggerType_OrchestratorTrigger_ReturnsOrchestratorTrigger()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"orchestrationTrigger","direction":"In","name":"ctx"}"""
        ]);
        Assert.Equal("orchestrationTrigger", metadata.GetDurableTriggerType());
    }

    [Fact]
    public void GetDurableTriggerType_ActivityTrigger_ReturnsActivityTrigger()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"activityTrigger","direction":"In","name":"input"}"""
        ]);
        Assert.Equal("activityTrigger", metadata.GetDurableTriggerType());
    }

    [Fact]
    public void GetDurableTriggerType_EntityTrigger_ReturnsEntityTrigger()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"entityTrigger","direction":"In","name":"dispatch"}"""
        ]);
        Assert.Equal("entityTrigger", metadata.GetDurableTriggerType());
    }

    [Fact]
    public void GetDurableTriggerType_NoDurableTrigger_ReturnsNull()
    {
        var metadata = CreateMetadata(
        [
            """{"type":"httpTrigger","direction":"In","name":"req"}"""
        ]);
        Assert.Null(metadata.GetDurableTriggerType());
    }

    // Use DefaultFunctionMetadata from the SDK (already referenced) to avoid re-implementing
    // the interface — SDK version changes could add/remove members.
    private static IFunctionMetadata CreateMetadata(IList<string>? rawBindings)
        => new DefaultFunctionMetadata
        {
            Name = "TestFunction",
            RawBindings = rawBindings?.ToList(),
        };
}

