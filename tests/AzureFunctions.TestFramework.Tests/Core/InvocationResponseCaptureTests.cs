using AzureFunctions.TestFramework.Core.Grpc;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

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
    public void BeginCapture_NoExtraOutputsOrUnmatchedRoute_ReturnsNull()
    {
        var service = CreateService(extraOutputs: false);
        Assert.Null(service.BeginCaptureInvocationResponse("id", "POST", "/api/items"));
        Assert.Null(CreateService().BeginCaptureInvocationResponse("id", "POST", "/api/unknown"));
        Assert.Null(CreateService().BeginCaptureInvocationResponse("id", "GET", "/api/items"));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("v1")]
    [InlineData("")]
    public async Task Capture_CompletesByInvocationIdWithHttpBindingName(string prefix)
    {
        var service = CreateService();
        var path = string.IsNullOrEmpty(prefix) ? "/items" : $"/{prefix}/items";
        using var capture = service.BeginCaptureInvocationResponse("id", "POST", path, prefix);
        Assert.NotNull(capture);
        Assert.Equal("HttpResponse", capture.HttpOutputBindingName);
        Assert.False(capture.WasDispatched);
        Assert.True(await service.SendInvocationRequestAsync("id", "POST", path, prefix));
        Assert.True(capture.WasDispatched);
        var response = SuccessResponse("id");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = response });
        Assert.Same(response, await capture.Response);
    }

    [Fact]
    public void Capture_DuplicateIdsFailAndDisposeReleasesRegistration()
    {
        var service = CreateService();
        var capture = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(capture);
        Assert.Throws<InvalidOperationException>(() =>
            service.BeginCaptureInvocationResponse("id", "POST", "/api/items"));
        capture.Dispose();
        using var next = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(next);
        capture.Dispose();
        Assert.Throws<InvalidOperationException>(() =>
            service.BeginCaptureInvocationResponse("id", "POST", "/api/items"));
    }

    [Fact]
    public async Task Capture_DisposeIgnoresLateResponse()
    {
        var service = CreateService();
        var capture = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(capture);
        capture.Dispose();
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = SuccessResponse("id") });
        Assert.False(capture.Response.IsCompleted);
    }

    [Fact]
    public async Task Capture_DisposedDispatchedIdRemainsReservedUntilResponse()
    {
        var service = CreateService();
        var capture = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(capture);
        await service.SendInvocationRequestAsync("id", "POST", "/api/items");
        capture.Dispose();

        Assert.Throws<InvalidOperationException>(() =>
            service.BeginCaptureInvocationResponse("id", "POST", "/api/items"));

        var originalResponse = SuccessResponse("id");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = originalResponse });

        using var retry = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(retry);
        Assert.False(retry.Response.IsCompleted);
        await service.SendInvocationRequestAsync("id", "POST", "/api/items");

        var retryResponse = SuccessResponse("id");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = retryResponse });
        Assert.Same(retryResponse, await retry.Response);
    }

    [Fact]
    public async Task Capture_ResponseConsumptionPreventsDisposedCaptureRemovingRetry()
    {
        var service = CreateService();
        var first = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(first);
        await service.SendInvocationRequestAsync("id", "POST", "/api/items");
        await service.HandleInvocationResponse(new StreamingMessage { InvocationResponse = SuccessResponse("id") });

        using var retry = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(retry);
        first.Dispose();
        Assert.Throws<InvalidOperationException>(() =>
            service.BeginCaptureInvocationResponse("id", "POST", "/api/items"));
    }

    [Fact]
    public async Task Capture_WriteFailureDoesNotMarkDispatched()
    {
        var service = CreateService();
        var capture = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(capture);
        typeof(GrpcHostService).GetField("_responseStream", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new RecordingWriter(_ => Task.FromException(new InvalidOperationException("write failed"))));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendInvocationRequestAsync("id", "POST", "/api/items"));
        Assert.False(capture.WasDispatched);
        capture.Dispose();
        using var retry = service.BeginCaptureInvocationResponse("id", "POST", "/api/items");
        Assert.NotNull(retry);
    }

    [Fact]
    public async Task Capture_SeparateInvocationsDoNotCrossTalk()
    {
        var service = CreateService();
        using var first = service.BeginCaptureInvocationResponse("first", "POST", "/api/items");
        using var second = service.BeginCaptureInvocationResponse("second", "POST", "/api/items");
        Assert.NotNull(first);
        Assert.NotNull(second);
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
