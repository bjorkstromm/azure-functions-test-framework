using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Core.Grpc;
using Google.Protobuf;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="GrpcHostService.ToTypedData"/>,
/// <see cref="GrpcHostService.ToParameterBinding"/>, and
/// <see cref="GrpcHostService.ProcessRawBinding"/>-related private logic.
/// </summary>
public class GrpcHostServiceBindingTests
{
    private readonly GrpcHostService _service;

    public GrpcHostServiceBindingTests()
    {
        _service = new GrpcHostService(
            NullLogger<GrpcHostService>.Instance,
            typeof(GrpcHostServiceBindingTests).Assembly);
    }

    // ── ToTypedData ────────────────────────────────────────────────────────────

    [Fact]
    public void ToTypedData_WithBytes_ReturnsTypedDataWithBytes()
    {
        var data = FunctionBindingData.WithBytes("param", new byte[] { 1, 2, 3 });
        var result = GrpcHostService.ToTypedData(data);
        Assert.Equal(TypedData.DataOneofCase.Bytes, result.DataCase);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Bytes.ToByteArray());
    }

    [Fact]
    public void ToTypedData_WithJson_ReturnsTypedDataWithJson()
    {
        var data = FunctionBindingData.WithJson("param", """{"key":"value"}""");
        var result = GrpcHostService.ToTypedData(data);
        Assert.Equal(TypedData.DataOneofCase.Json, result.DataCase);
        Assert.Equal("""{"key":"value"}""", result.Json);
    }

    [Fact]
    public void ToTypedData_WithString_ReturnsTypedDataWithString()
    {
        var data = FunctionBindingData.WithString("param", "hello");
        var result = GrpcHostService.ToTypedData(data);
        Assert.Equal(TypedData.DataOneofCase.String, result.DataCase);
        Assert.Equal("hello", result.String);
    }

    [Fact]
    public void ToTypedData_WithModelBindingData_ReturnsModelBindingData()
    {
        var mbd = new ModelBindingDataValue
        {
            Source = "test-source",
            ContentType = "application/json",
            Content = new byte[] { 10, 20 }
        };
        var data = FunctionBindingData.WithModelBindingData("param", mbd);
        var result = GrpcHostService.ToTypedData(data);

        Assert.Equal(TypedData.DataOneofCase.ModelBindingData, result.DataCase);
        Assert.Equal("test-source", result.ModelBindingData.Source);
        Assert.Equal("application/json", result.ModelBindingData.ContentType);
    }

    [Fact]
    public void ToTypedData_WithCollectionModelBindingData_ReturnsCollection()
    {
        var items = new List<ModelBindingDataValue>
        {
            new() { Source = "s1", ContentType = "text/plain", Content = new byte[] { 1 } },
            new() { Source = "s2", ContentType = "text/plain", Content = new byte[] { 2 } }
        };
        var data = FunctionBindingData.WithCollectionModelBindingData("param", items);
        var result = GrpcHostService.ToTypedData(data);

        Assert.Equal(TypedData.DataOneofCase.CollectionModelBindingData, result.DataCase);
        Assert.Equal(2, result.CollectionModelBindingData.ModelBindingData.Count);
    }

    [Fact]
    public void ToTypedData_NoValueSet_ReturnsEmptyTypedData()
    {
        // All properties null → should return empty TypedData
        var data = new FunctionBindingData { Name = "param" };
        var result = GrpcHostService.ToTypedData(data);
        Assert.Equal(TypedData.DataOneofCase.None, result.DataCase);
    }

    // ── ToParameterBinding ────────────────────────────────────────────────────

    [Fact]
    public void ToParameterBinding_SetsNameAndData()
    {
        var data = FunctionBindingData.WithString("myParam", "hello");
        var binding = GrpcHostService.ToParameterBinding(data);

        Assert.Equal("myParam", binding.Name);
        Assert.Equal(TypedData.DataOneofCase.String, binding.Data.DataCase);
        Assert.Equal("hello", binding.Data.String);
    }

    // ── GetSyntheticInputParameters ────────────────────────────────────────────

    [Fact]
    public void GetSyntheticInputParameters_UnknownFunctionId_ReturnsEmpty()
    {
        var result = _service.GetSyntheticInputParameters("unknown-id");
        Assert.Empty(result);
    }

    // ── GetHttpTriggerBindingName ─────────────────────────────────────────────

    [Fact]
    public void GetHttpTriggerBindingName_UnknownFunctionId_ReturnsDefaultReq()
    {
        var result = _service.GetHttpTriggerBindingName("unknown-fn-id");
        Assert.Equal("req", result);
    }

    // ── GetFunctionRegistration ───────────────────────────────────────────────

    [Fact]
    public void GetFunctionRegistration_UnknownFunction_ReturnsNull()
    {
        var result = _service.GetFunctionRegistration("NonExistent");
        Assert.Null(result);
    }

    // ── FindFunctionMatch ─────────────────────────────────────────────────────

    [Fact]
    public void FindFunctionMatch_NoRoutesRegistered_ReturnsNullFunctionId()
    {
        var (fnId, _) = _service.FindFunctionMatch("GET", "/api/todos");
        Assert.Null(fnId);
    }

    [Fact]
    public void FindFunctionId_NoRoutesRegistered_ReturnsNull()
    {
        var result = _service.FindFunctionId("GET", "/api/todos");
        Assert.Null(result);
    }

    // ── FindFunctionMatch with route prefix stripping ────────────────────────

    [Fact]
    public void FindFunctionMatch_StripsApiPrefix()
    {
        // Manually add a route to RouteMatcher via reflection to simulate loaded function
        var routeMatcher = _service.RouteMatcher;
        routeMatcher.AddRoute("GET", "todos", "fn-id-1");

        var (fnId, _) = _service.FindFunctionMatch("GET", "/api/todos");
        Assert.Equal("fn-id-1", fnId);
    }

    [Fact]
    public void FindFunctionMatch_StripsQueryString()
    {
        var routeMatcher = _service.RouteMatcher;
        routeMatcher.AddRoute("GET", "todos", "fn-id-2");

        var (fnId, _) = _service.FindFunctionMatch("GET", "/api/todos?page=1");
        Assert.Equal("fn-id-2", fnId);
    }

    [Fact]
    public void FindFunctionMatch_CustomRoutePrefix_Stripped()
    {
        var routeMatcher = _service.RouteMatcher;
        routeMatcher.AddRoute("GET", "items", "fn-id-3");

        var (fnId, _) = _service.FindFunctionMatch("GET", "/v1/items", routePrefix: "v1");
        Assert.Equal("fn-id-3", fnId);
    }

    [Fact]
    public void FindFunctionMatch_EmptyRoutePrefix_DoesNotStrip()
    {
        var routeMatcher = _service.RouteMatcher;
        routeMatcher.AddRoute("GET", "ping", "fn-id-4");

        var (fnId, _) = _service.FindFunctionMatch("GET", "ping", routePrefix: "");
        Assert.Equal("fn-id-4", fnId);
    }

    // ── CreateInvocationResult ────────────────────────────────────────────────

    [Fact]
    public void CreateInvocationResult_Success_ReturnsSuccessResult()
    {
        var invResponse = new InvocationResponse
        {
            InvocationId = "inv-1",
            Result = new StatusResult { Status = StatusResult.Types.Status.Success }
        };
        var result = InvokeCreateInvocationResult("inv-1", invResponse);

        Assert.True(result.Success);
        Assert.Equal("inv-1", result.InvocationId);
        Assert.Null(result.Error);
    }

    [Fact]
    public void CreateInvocationResult_Failure_ReturnsFailureResult()
    {
        var invResponse = new InvocationResponse
        {
            InvocationId = "inv-2",
            Result = new StatusResult
            {
                Status = StatusResult.Types.Status.Failure,
                Exception = new RpcException { Message = "oops" }
            }
        };
        var result = InvokeCreateInvocationResult("inv-2", invResponse);

        Assert.False(result.Success);
        Assert.Equal("oops", result.Error);
    }

    [Fact]
    public void CreateInvocationResult_NullResponse_ReturnsFailureResult()
    {
        var result = InvokeCreateInvocationResult("inv-3", null);

        Assert.False(result.Success);
        Assert.Empty(result.OutputData);
        Assert.Null(result.ReturnValue);
    }

    [Fact]
    public void CreateInvocationResult_WithOutputData_PopulatesOutputData()
    {
        var invResponse = new InvocationResponse
        {
            InvocationId = "inv-4",
            Result = new StatusResult { Status = StatusResult.Types.Status.Success }
        };
        invResponse.OutputData.Add(new ParameterBinding
        {
            Name = "outputQueue",
            Data = new TypedData { String = "queued-message" }
        });

        var result = InvokeCreateInvocationResult("inv-4", invResponse);

        Assert.True(result.OutputData.ContainsKey("outputQueue"));
        Assert.Equal("queued-message", result.OutputData["outputQueue"]);
    }

    [Fact]
    public void CreateInvocationResult_WithReturnValue_PopulatesReturnValue()
    {
        var invResponse = new InvocationResponse
        {
            InvocationId = "inv-5",
            Result = new StatusResult { Status = StatusResult.Types.Status.Success },
            ReturnValue = new TypedData { String = "return-val" }
        };

        var result = InvokeCreateInvocationResult("inv-5", invResponse);

        Assert.Equal("return-val", result.ReturnValue);
    }

    // ── ProcessRawBinding ────────────────────────────────────────────────────

    [Fact]
    public void ProcessRawBinding_HttpTrigger_PopulatesRouteMatcher()
    {
        var metadata = CreateRpcFunctionMetadata(
            "HttpFunc",
            "http-fn-1",
            """{"type":"httpTrigger","direction":"In","name":"req","methods":["GET"],"route":"mytodos"}"""
        );
        InvokeProcessRawBinding(metadata, metadata.RawBindings[0]);

        Assert.True(_service.FunctionRouteMap.ContainsKey("GET:mytodos"));
    }

    [Fact]
    public void ProcessRawBinding_HttpTrigger_NoRoute_UsesDefaultFunctionName()
    {
        var metadata = CreateRpcFunctionMetadata(
            "NoRouteFunc",
            "http-fn-2",
            """{"type":"httpTrigger","direction":"In","name":"req"}"""
        );
        InvokeProcessRawBinding(metadata, metadata.RawBindings[0]);

        // Should default to function name as route
        Assert.Contains(_service.FunctionRouteMap, kv => kv.Key.EndsWith(":NoRouteFunc", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessRawBinding_HttpTrigger_NoMethods_RegistersAllMethods()
    {
        var metadata = CreateRpcFunctionMetadata(
            "AllMethodsFunc",
            "http-fn-3",
            """{"type":"httpTrigger","direction":"In","name":"req","route":"allroute"}"""
        );
        InvokeProcessRawBinding(metadata, metadata.RawBindings[0]);

        // Should register all standard HTTP methods
        Assert.True(_service.FunctionRouteMap.ContainsKey("GET:allroute"));
        Assert.True(_service.FunctionRouteMap.ContainsKey("POST:allroute"));
        Assert.True(_service.FunctionRouteMap.ContainsKey("DELETE:allroute"));
    }

    [Fact]
    public void ProcessRawBinding_TimerTrigger_RegistersFunction()
    {
        var metadata = CreateRpcFunctionMetadata(
            "TimerFunc",
            "timer-fn-1",
            """{"type":"timerTrigger","direction":"In","name":"myTimer","schedule":"0 * * * * *"}"""
        );
        InvokeProcessRawBinding(metadata, metadata.RawBindings[0]);

        var reg = _service.GetFunctionRegistration("TimerFunc");
        Assert.NotNull(reg);
        Assert.Equal("timerTrigger", reg.TriggerType);
        Assert.Equal("myTimer", reg.ParameterName);
    }

    [Fact]
    public void ProcessRawBinding_QueueTrigger_RegistersFunction()
    {
        var metadata = CreateRpcFunctionMetadata(
            "QueueFunc",
            "queue-fn-1",
            """{"type":"queueTrigger","direction":"In","name":"myQueueItem","queueName":"myqueue"}"""
        );
        InvokeProcessRawBinding(metadata, metadata.RawBindings[0]);

        var reg = _service.GetFunctionRegistration("QueueFunc");
        Assert.NotNull(reg);
        Assert.Equal("queueTrigger", reg.TriggerType);
        Assert.Equal("myQueueItem", reg.ParameterName);
    }

    [Fact]
    public void ProcessRawBinding_MalformedJson_DoesNotThrow()
    {
        var metadata = CreateRpcFunctionMetadata("BadFunc", "bad-fn-1", "NOT-VALID-JSON");

        // Should not throw
        InvokeProcessRawBinding(metadata, metadata.RawBindings[0]);
    }

    [Fact]
    public void ProcessRawBinding_NoTypeProperty_DoesNotRegister()
    {
        var metadata = CreateRpcFunctionMetadata(
            "NoTypeFunc",
            "notype-fn",
            """{"direction":"In","name":"param"}"""
        );
        InvokeProcessRawBinding(metadata, metadata.RawBindings[0]);

        Assert.Null(_service.GetFunctionRegistration("NoTypeFunc"));
    }

    [Fact]
    public void ProcessRawBinding_WithSyntheticProvider_RegistersSyntheticParam()
    {
        var provider = new FakeSyntheticProvider("durableClient");
        var service = new GrpcHostService(
            NullLogger<GrpcHostService>.Instance,
            typeof(GrpcHostServiceBindingTests).Assembly,
            [provider]);

        var metadata = CreateRpcFunctionMetadata(
            "DurableFunc",
            "durable-fn-1",
            """{"type":"durableClient","direction":"In","name":"client"}"""
        );
        InvokeProcessRawBinding(service, metadata, metadata.RawBindings[0]);

        var synth = service.GetSyntheticInputParameters("durable-fn-1");
        Assert.Single(synth);
        Assert.Equal("client", synth[0].Name);
    }

    [Fact]
    public void ProcessRawBinding_WithSyntheticProvider_ProviderReturnsNull_SkipsBinding()
    {
        var provider = new FakeSyntheticProvider("durableClient", returnNull: true);
        var service = new GrpcHostService(
            NullLogger<GrpcHostService>.Instance,
            typeof(GrpcHostServiceBindingTests).Assembly,
            [provider]);

        var metadata = CreateRpcFunctionMetadata(
            "DurableNullFunc",
            "durable-null-fn",
            """{"type":"durableClient","direction":"In","name":"client"}"""
        );
        InvokeProcessRawBinding(service, metadata, metadata.RawBindings[0]);

        var synth = service.GetSyntheticInputParameters("durable-null-fn");
        Assert.Empty(synth);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RpcFunctionMetadata CreateRpcFunctionMetadata(
        string name, string functionId, string rawBinding)
    {
        var m = new RpcFunctionMetadata
        {
            Name = name,
            FunctionId = functionId,
            EntryPoint = $"TestAssembly.{name}",
            ScriptFile = "TestAssembly.dll"
        };
        m.RawBindings.Add(rawBinding);
        return m;
    }

    private void InvokeProcessRawBinding(RpcFunctionMetadata metadata, string rawBinding)
        => InvokeProcessRawBinding(_service, metadata, rawBinding);

    private static void InvokeProcessRawBinding(
        GrpcHostService service, RpcFunctionMetadata metadata, string rawBinding)
    {
        var method = typeof(GrpcHostService)
            .GetMethod("ProcessRawBinding", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(service, [metadata, rawBinding]);
    }

    private static FunctionInvocationResult InvokeCreateInvocationResult(
        string invocationId, InvocationResponse? response)
    {
        var method = typeof(GrpcHostService)
            .GetMethod("CreateInvocationResult", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (FunctionInvocationResult)method.Invoke(null, [invocationId, response])!;
    }

    private sealed class FakeSyntheticProvider : ISyntheticBindingProvider
    {
        private readonly bool _returnNull;

        public FakeSyntheticProvider(string bindingType, bool returnNull = false)
        {
            BindingType = bindingType;
            _returnNull = returnNull;
        }

        public string BindingType { get; }

        public FunctionBindingData? CreateSyntheticParameter(string parameterName, JsonElement bindingConfig)
        {
            if (_returnNull) return null;
            return FunctionBindingData.WithString(parameterName, "fake-value");
        }
    }
}
