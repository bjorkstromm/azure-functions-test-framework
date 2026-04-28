using AzureFunctions.TestFramework.Dapr;
using AzureFunctions.TestFramework.Core;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Dapr;

/// <summary>
/// Unit tests for <see cref="FunctionsTestHostDaprExtensions"/> internal binding-data factories
/// and <see cref="FunctionsTestHostDaprExtensions.GetJsonFromContext"/>.
/// </summary>
public class FunctionsTestHostDaprExtensionsBindingDataTests
{
    private static FunctionRegistration MakeRegistration(string paramName = "payload") =>
        new("func-id", "TestFunction", "daprServiceInvocationTrigger", paramName);

    // -------------------------------------------------------------------------
    // CreateInvocationBindingDataFromJson
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateInvocationBindingDataFromJson_WithJson_ReturnsJsonBinding()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "daprServiceInvocationTrigger",
            InputData = { ["$daprInvocationJson"] = """{"name":"Alice"}""" }
        };

        var result = FunctionsTestHostDaprExtensions.CreateInvocationBindingDataFromJson(context, MakeRegistration());

        Assert.Single(result.InputData);
        var binding = result.InputData[0];
        Assert.Equal("payload", binding.Name);
        Assert.Equal("""{"name":"Alice"}""", binding.Json);
    }

    [Fact]
    public void CreateInvocationBindingDataFromJson_MissingKey_ReturnsEmptyJsonObject()
    {
        var context = new FunctionInvocationContext { TriggerType = "daprServiceInvocationTrigger" };

        var result = FunctionsTestHostDaprExtensions.CreateInvocationBindingDataFromJson(context, MakeRegistration());

        Assert.Single(result.InputData);
        Assert.Equal("{}", result.InputData[0].Json);
    }

    [Fact]
    public void CreateInvocationBindingDataFromJson_NullValueInContext_ReturnsEmptyJsonObject()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "daprServiceInvocationTrigger",
            InputData = { ["$daprInvocationJson"] = null! }
        };

        var result = FunctionsTestHostDaprExtensions.CreateInvocationBindingDataFromJson(context, MakeRegistration());

        Assert.Equal("{}", result.InputData[0].Json);
    }

    // -------------------------------------------------------------------------
    // CreateTopicBindingDataFromJson
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateTopicBindingDataFromJson_WithJson_ReturnsJsonBinding()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "daprTopicTrigger",
            InputData = { ["$daprTopicJson"] = """{"event":"test"}""" }
        };

        var result = FunctionsTestHostDaprExtensions.CreateTopicBindingDataFromJson(context, MakeRegistration("message"));

        Assert.Single(result.InputData);
        Assert.Equal("message", result.InputData[0].Name);
        Assert.Equal("""{"event":"test"}""", result.InputData[0].Json);
    }

    [Fact]
    public void CreateTopicBindingDataFromJson_MissingKey_ReturnsEmptyJsonObject()
    {
        var context = new FunctionInvocationContext { TriggerType = "daprTopicTrigger" };

        var result = FunctionsTestHostDaprExtensions.CreateTopicBindingDataFromJson(context, MakeRegistration());

        Assert.Single(result.InputData);
        Assert.Equal("{}", result.InputData[0].Json);
    }

    [Fact]
    public void CreateTopicBindingDataFromJson_NullValueInContext_ReturnsEmptyJsonObject()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "daprTopicTrigger",
            InputData = { ["$daprTopicJson"] = null! }
        };

        var result = FunctionsTestHostDaprExtensions.CreateTopicBindingDataFromJson(context, MakeRegistration());

        Assert.Equal("{}", result.InputData[0].Json);
    }

    [Fact]
    public void CreateTopicBindingDataFromJson_UsesCorrectParameterName()
    {
        var context = new FunctionInvocationContext
        {
            TriggerType = "daprTopicTrigger",
            InputData = { ["$daprTopicJson"] = "{}" }
        };
        var reg = MakeRegistration("topicMessage");

        var result = FunctionsTestHostDaprExtensions.CreateTopicBindingDataFromJson(context, reg);

        Assert.Equal("topicMessage", result.InputData[0].Name);
    }
}
