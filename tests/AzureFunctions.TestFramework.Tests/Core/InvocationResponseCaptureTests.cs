using AzureFunctions.TestFramework.Core.Grpc;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Covers <see cref="InvocationResponseCapture"/>'s explicit lifecycle:
/// Pending → Dispatched → Completed, or → Abandoned when disposed before a response arrives.
/// </summary>
public class InvocationResponseCaptureTests
{
    internal static GrpcHostService CreateService(bool extraOutputs = true, string route = "items")
    {
        var service = new GrpcHostService(
            NullLogger<GrpcHostService>.Instance, typeof(InvocationResponseCaptureTests).Assembly);
        var metadata = new RpcFunctionMetadata { Name = "MixedOutputs", FunctionId = "mixed" };
        var bindings = new List<string>
        {
            $$"""{"type":"httpTrigger","direction":"in","name":"req","route":"{{route}}","methods":["post"]}""",
            """{"type":"http","direction":"out","name":"HttpResponse"}"""
        };
        if (extraOutputs)
        {
            bindings.Add("""{"type":"queue","direction":"out","name":"QueueMessage"}""");
        }
        var process = typeof(GrpcHostService).GetMethod("ProcessRawBinding", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var binding in bindings)
        {
            process.Invoke(service, [metadata, binding]);
        }
        typeof(GrpcHostService).GetField("_responseStream", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new RecordingWriter());
        return service;
    }

    [Fact]
    public void BeginCapture_DuplicateIdThrows()
    {
        var service = CreateService();
        using var capture = service.BeginCaptureInvocationResponse("id");
        Assert.Equal(InvocationResponseCaptureState.Pending, capture.State);
        Assert.Throws<InvalidOperationException>(() => service.BeginCaptureInvocationResponse("id"));
    }

    [Fact]
    public async Task Capture_Dispatched_SetsOutputInfoThenCompletesWithResponse()
    {
        var service = CreateService();
        using var capture = service.BeginCaptureInvocationResponse("id");
        Assert.Null(capture.OutputInfo);

        Assert.True(await service.SendInvocationRequestAsync("id", "POST", "/api/items"));
        Assert.Equal(InvocationResponseCaptureState.Dispatched, capture.State);
        Assert.Equal("HttpResponse", capture.OutputInfo!.HttpOutputBindingName);
        Assert.True(capture.OutputInfo.HasNonHttpOutputs);
        Assert.False(capture.OutputInfo.HasNonHttpReturnValue);

        var response = SuccessResponse("id");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = response });
        Assert.Equal(InvocationResponseCaptureState.Completed, capture.State);
        Assert.Same(response, await capture.Response);
    }

    [Fact]
    public async Task Capture_PendingDispose_FreesIdImmediatelyAndIgnoresLateResponse()
    {
        var service = CreateService();
        var capture = service.BeginCaptureInvocationResponse("id");
        capture.Dispose();

        // Never dispatched: a late/stray response for the disposed ID matches nothing and is dropped.
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = SuccessResponse("id") });
        Assert.False(capture.Response.IsCompleted);

        // The ID is free again right away — a genuine new request can reuse it and complete normally.
        using var next = service.BeginCaptureInvocationResponse("id");
        Assert.False(next.Response.IsCompleted);
        var response = SuccessResponse("id");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = response });
        Assert.Same(response, await next.Response);
    }

    [Fact]
    public async Task Capture_DispatchedDispose_ReservesIdUntilResponseConsumedThenAllowsRetry()
    {
        var service = CreateService();
        var capture = service.BeginCaptureInvocationResponse("id");
        await service.SendInvocationRequestAsync("id", "POST", "/api/items");
        capture.Dispose();
        Assert.Equal(InvocationResponseCaptureState.Abandoned, capture.State);

        // Still reserved: a retry with the same ID must not silently shadow the original.
        Assert.Throws<InvalidOperationException>(() => service.BeginCaptureInvocationResponse("id"));

        var originalResponse = SuccessResponse("id");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = originalResponse });

        // Once the original response is consumed, the ID is free for a genuine retry.
        using var retry = service.BeginCaptureInvocationResponse("id");
        Assert.False(retry.Response.IsCompleted);
        await service.SendInvocationRequestAsync("id", "POST", "/api/items");

        var retryResponse = SuccessResponse("id");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = retryResponse });
        Assert.Same(retryResponse, await retry.Response);
    }

    [Fact]
    public async Task Capture_WriteFailureLeavesPending_DisposeAllowsImmediateRetry()
    {
        var service = CreateService();
        var capture = service.BeginCaptureInvocationResponse("id");
        typeof(GrpcHostService).GetField("_responseStream", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new RecordingWriter(_ => Task.FromException(new InvalidOperationException("write failed"))));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendInvocationRequestAsync("id", "POST", "/api/items"));
        Assert.Equal(InvocationResponseCaptureState.Pending, capture.State);

        capture.Dispose();
        using var retry = service.BeginCaptureInvocationResponse("id");
        Assert.NotNull(retry);
    }

    [Fact]
    public async Task Capture_SeparateInvocationsDoNotCrossTalk()
    {
        var service = CreateService();
        using var first = service.BeginCaptureInvocationResponse("first");
        using var second = service.BeginCaptureInvocationResponse("second");

        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = SuccessResponse("second") });
        Assert.False(first.Response.IsCompleted);
        Assert.Equal("second", (await second.Response).InvocationId);
    }

    internal static InvocationResponse SuccessResponse(string id) => new()
    {
        InvocationId = id,
        Result = new StatusResult { Status = StatusResult.Types.Status.Success },
        OutputData =
        {
            new ParameterBinding { Name = "HttpResponse", Data = new TypedData() },
            new ParameterBinding { Name = "QueueMessage", Data = new TypedData { String = "queued" } }
        }
    };

    private sealed class RecordingWriter(Func<StreamingMessage, Task>? write = null) : IServerStreamWriter<StreamingMessage>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(StreamingMessage message) => write?.Invoke(message) ?? Task.CompletedTask;
    }
}
