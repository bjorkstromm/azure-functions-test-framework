using AzureFunctions.TestFramework.Durable;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.UnitTests.Durable;

/// <summary>
/// Unit tests for <see cref="DurableOrchestrationStatus"/>.
/// </summary>
public class DurableOrchestrationStatusTests
{
    [Theory]
    [InlineData("Completed", true)]
    [InlineData("Failed", true)]
    [InlineData("Terminated", true)]
    [InlineData("Canceled", true)]
    [InlineData("Running", false)]
    [InlineData("Pending", false)]
    [InlineData("Suspended", false)]
    [InlineData(null, false)]
    public void IsTerminal_ReturnsExpected(string? runtimeStatus, bool expected)
    {
        var status = new DurableOrchestrationStatus { RuntimeStatus = runtimeStatus };
        Assert.Equal(expected, status.IsTerminal);
    }

    [Fact]
    public void ReadOutputAsString_StringKind_ReturnsString()
    {
        var status = new DurableOrchestrationStatus
        {
            Output = JsonDocument.Parse("""  "hello world"  """).RootElement.Clone()
        };
        Assert.Equal("hello world", status.ReadOutputAsString());
    }

    [Fact]
    public void ReadOutputAsString_NullKind_ReturnsNull()
    {
        var status = new DurableOrchestrationStatus
        {
            Output = JsonDocument.Parse("null").RootElement.Clone()
        };
        Assert.Null(status.ReadOutputAsString());
    }

    [Fact]
    public void ReadOutputAsString_UndefinedKind_ReturnsNull()
    {
        var status = new DurableOrchestrationStatus();
        // Default JsonElement has ValueKind == Undefined
        Assert.Null(status.ReadOutputAsString());
    }

    [Fact]
    public void ReadOutputAsString_ObjectKind_ReturnsRawText()
    {
        var status = new DurableOrchestrationStatus
        {
            Output = JsonDocument.Parse("""{"key":42}""").RootElement.Clone()
        };
        var result = status.ReadOutputAsString();
        Assert.Contains("key", result);
        Assert.Contains("42", result);
    }

    [Fact]
    public void ReadOutputAsString_ArrayKind_ReturnsRawText()
    {
        var status = new DurableOrchestrationStatus
        {
            Output = JsonDocument.Parse("[1,2,3]").RootElement.Clone()
        };
        var result = status.ReadOutputAsString();
        Assert.NotNull(result);
        Assert.Contains("1", result);
    }

    [Fact]
    public void ReadCustomStatusAs_NullKind_ReturnsDefault()
    {
        var status = new DurableOrchestrationStatus
        {
            CustomStatus = JsonDocument.Parse("null").RootElement.Clone()
        };
        Assert.Null(status.ReadCustomStatusAs<string>());
    }

    [Fact]
    public void ReadCustomStatusAs_UndefinedKind_ReturnsDefault()
    {
        var status = new DurableOrchestrationStatus();
        Assert.Null(status.ReadCustomStatusAs<string>());
    }

    [Fact]
    public void ReadCustomStatusAs_ObjectKind_Deserializes()
    {
        var status = new DurableOrchestrationStatus
        {
            CustomStatus = JsonDocument.Parse("""{"Step":3}""").RootElement.Clone()
        };
        var result = status.ReadCustomStatusAs<CustomStatusDto>();
        Assert.NotNull(result);
        Assert.Equal(3, result!.Step);
    }

    private sealed class CustomStatusDto
    {
        public int Step { get; set; }
    }
}
