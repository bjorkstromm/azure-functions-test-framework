using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for the per-case message-handler methods of <see cref="GrpcHostService"/>.
/// </summary>
public class GrpcHostServiceHandleMessageTests
{
    private readonly GrpcHostService _service;

    public GrpcHostServiceHandleMessageTests()
    {
        _service = new GrpcHostService(
            NullLogger<GrpcHostService>.Instance,
            typeof(GrpcHostServiceHandleMessageTests).Assembly);
    }

    // ── HandleWorkerInitResponse ─────────────────────────────────────────────

    [Fact]
    public async Task HandleWorkerInitResponse_SetsTcs()
    {
        var tcs = new TaskCompletionSource<StreamingMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SetPrivateField(_service, "_workerInitTcs", tcs);

        var message = new StreamingMessage { WorkerInitResponse = new WorkerInitResponse() };

        await _service.HandleWorkerInitResponse(message);

        Assert.True(tcs.Task.IsCompleted);
        Assert.Equal(message, await tcs.Task);
    }

    [Fact]
    public async Task HandleWorkerInitResponse_NullTcs_DoesNotThrow()
    {
        // _workerInitTcs is null by default
        SetPrivateField(_service, "_workerInitTcs", null);
        var message = new StreamingMessage { WorkerInitResponse = new WorkerInitResponse() };

        // Should not throw even when _workerInitTcs is null
        await _service.HandleWorkerInitResponse(message);
    }

    // ── HandleLoadOrMetadataResponse ─────────────────────────────────────────

    [Fact]
    public async Task HandleLoadOrMetadataResponse_FunctionLoadResponse_CompletesPending()
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = AddPendingRequest(_service, requestId);

        var message = new StreamingMessage
        {
            RequestId = requestId,
            FunctionLoadResponse = new FunctionLoadResponse()
        };

        await _service.HandleLoadOrMetadataResponse(message);

        Assert.True(tcs.Task.IsCompleted);
        Assert.Equal(message, await tcs.Task);
    }

    [Fact]
    public async Task HandleLoadOrMetadataResponse_UnknownRequestId_DoesNotThrow()
    {
        var message = new StreamingMessage
        {
            RequestId = "unknown-id",
            FunctionMetadataResponse = new FunctionMetadataResponse()
        };

        // Should not throw for an unregistered request ID
        await _service.HandleLoadOrMetadataResponse(message);
    }

    // ── HandleInvocationResponse ─────────────────────────────────────────────

    [Fact]
    public async Task HandleInvocationResponse_Success_CompletesPending()
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = AddPendingRequest(_service, requestId);

        var message = new StreamingMessage
        {
            RequestId = requestId,
            InvocationResponse = new InvocationResponse
            {
                InvocationId = requestId,
                Result = new StatusResult { Status = StatusResult.Types.Status.Success }
            }
        };

        await _service.HandleInvocationResponse(message);

        Assert.True(tcs.Task.IsCompleted);
        Assert.Equal(message, await tcs.Task);
    }

    [Fact]
    public async Task HandleInvocationResponse_Failure_CompletesPendingAndDoesNotThrow()
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = AddPendingRequest(_service, requestId);

        var message = new StreamingMessage
        {
            RequestId = requestId,
            InvocationResponse = new InvocationResponse
            {
                InvocationId = requestId,
                Result = new StatusResult
                {
                    Status = StatusResult.Types.Status.Failure,
                    Exception = new RpcException { Message = "Something went wrong" }
                }
            }
        };

        await _service.HandleInvocationResponse(message);

        Assert.True(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task HandleInvocationResponse_NullInvocationResponse_DoesNotThrow()
    {
        var message = new StreamingMessage
        {
            RequestId = Guid.NewGuid().ToString()
            // InvocationResponse is null / not set
        };

        await _service.HandleInvocationResponse(message);
    }

    // ── LogInvocationFailure ──────────────────────────────────────────────────

    [Fact]
    public void LogInvocationFailure_NullResponse_DoesNotThrow()
    {
        // Should handle null gracefully (uses ?? "unknown")
        _service.LogInvocationFailure(null);
    }

    [Fact]
    public void LogInvocationFailure_ResponseWithExceptionMessage_DoesNotThrow()
    {
        var response = new InvocationResponse
        {
            InvocationId = Guid.NewGuid().ToString(),
            Result = new StatusResult
            {
                Status = StatusResult.Types.Status.Failure,
                Exception = new RpcException { Message = "Boom" }
            }
        };

        _service.LogInvocationFailure(response);
    }

    [Fact]
    public void LogInvocationFailure_ResponseWithNullException_UsesUnknown()
    {
        var response = new InvocationResponse
        {
            InvocationId = Guid.NewGuid().ToString(),
            Result = new StatusResult { Status = StatusResult.Types.Status.Failure }
            // Exception is null
        };

        // Should not throw; uses "unknown" as fallback
        _service.LogInvocationFailure(response);
    }

    // ── HandleRpcLogMessage ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleRpcLogMessage_ReturnsCompletedTask()
    {
        var message = new StreamingMessage
        {
            RpcLog = new RpcLog
            {
                Level = RpcLog.Types.Level.Information,
                Message = "hello from worker"
            }
        };

        await _service.HandleRpcLogMessage(message);
    }

    // ── HandleUnknownMessage ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleUnknownMessage_DoesNotThrow()
    {
        var message = new StreamingMessage();

        await _service.HandleUnknownMessage(message);
    }

    // ── HandleWorkerMessageAsync dispatch ────────────────────────────────────

    [Fact]
    public async Task HandleWorkerMessageAsync_WorkerInitResponse_DispatchesToHandler()
    {
        var tcs = new TaskCompletionSource<StreamingMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SetPrivateField(_service, "_workerInitTcs", tcs);

        var message = new StreamingMessage { WorkerInitResponse = new WorkerInitResponse() };

        await _service.HandleWorkerMessageAsync(message, CancellationToken.None);

        Assert.True(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_InvocationResponse_DispatchesToHandler()
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = AddPendingRequest(_service, requestId);

        var message = new StreamingMessage
        {
            RequestId = requestId,
            InvocationResponse = new InvocationResponse
            {
                InvocationId = requestId,
                Result = new StatusResult { Status = StatusResult.Types.Status.Success }
            }
        };

        await _service.HandleWorkerMessageAsync(message, CancellationToken.None);

        Assert.True(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_FunctionLoadResponse_DispatchesToHandler()
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = AddPendingRequest(_service, requestId);

        var message = new StreamingMessage
        {
            RequestId = requestId,
            FunctionLoadResponse = new FunctionLoadResponse()
        };

        await _service.HandleWorkerMessageAsync(message, CancellationToken.None);

        Assert.True(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_FunctionMetadataResponse_DispatchesToHandler()
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = AddPendingRequest(_service, requestId);

        var message = new StreamingMessage
        {
            RequestId = requestId,
            FunctionMetadataResponse = new FunctionMetadataResponse()
        };

        await _service.HandleWorkerMessageAsync(message, CancellationToken.None);

        Assert.True(tcs.Task.IsCompleted);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_RpcLog_DoesNotThrow()
    {
        var message = new StreamingMessage
        {
            RpcLog = new RpcLog { Level = RpcLog.Types.Level.Warning, Message = "warn" }
        };

        await _service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_UnknownContentCase_DoesNotThrow()
    {
        // Default StreamingMessage has ContentCase == None (0)
        var message = new StreamingMessage();

        await _service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TaskCompletionSource<StreamingMessage> AddPendingRequest(
        GrpcHostService service,
        string requestId)
    {
        var tcs = new TaskCompletionSource<StreamingMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var field = typeof(GrpcHostService)
            .GetField("_pendingRequests", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var pending = (Dictionary<string, TaskCompletionSource<StreamingMessage>>)field.GetValue(service)!;
        lock (pending)
        {
            pending[requestId] = tcs;
        }
        return tcs;
    }

    private static void SetPrivateField(object obj, string fieldName, object? value)
    {
        var field = obj.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(obj, value);
    }
}
