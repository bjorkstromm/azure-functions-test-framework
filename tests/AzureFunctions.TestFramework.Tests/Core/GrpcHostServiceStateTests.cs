using AzureFunctions.TestFramework.Core;
using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Tests for GrpcHostService connection state management, message routing,
/// and other internal behaviors.
/// </summary>
public class GrpcHostServiceStateTests
{
    private GrpcHostService CreateService() =>
        new(NullLogger<GrpcHostService>.Instance,
            typeof(GrpcHostServiceStateTests).Assembly);

    // ── WaitForConnectionAsync ────────────────────────────────────────────────

    [Fact]
    public async Task WaitForConnectionAsync_AlreadyAheadVersion_CompletesImmediately()
    {
        var service = CreateService();

        // ConnectionVersion starts at 0; previousConnectionVersion=-1 means "never connected"
        // But if we increment manually: simulate a connection
        // The default connection version is 0, so providing previousConnectionVersion=-1 means
        // the current version (0) is already > -1, so it should return immediately.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Should complete synchronously or nearly so — not throw OperationCanceledException
        await service.WaitForConnectionAsync(-1, cts.Token);
    }

    [Fact]
    public async Task WaitForConnectionAsync_SameVersion_WaitsUntilCancelled()
    {
        var service = CreateService();

        // ConnectionVersion is 0; previousConnectionVersion=0 means it needs a new connection.
        // Cancelling the token should terminate the wait.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.WaitForConnectionAsync(0, cts.Token));
    }

    // ── ConnectionVersion ─────────────────────────────────────────────────────

    [Fact]
    public void ConnectionVersion_Initial_IsZero()
    {
        var service = CreateService();
        Assert.Equal(0, service.ConnectionVersion);
    }

    // ── IsConnected ───────────────────────────────────────────────────────────

    [Fact]
    public void IsConnected_Initially_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsConnected);
    }

    // ── IsFunctionsLoaded ─────────────────────────────────────────────────────

    [Fact]
    public void IsFunctionsLoaded_Initially_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.IsFunctionsLoaded);
    }

    // ── RequestShutdown / SignalShutdown ──────────────────────────────────────

    [Fact]
    public async Task SignalShutdownAsync_WithNoActiveStream_CompletesAfterTimeout()
    {
        var service = CreateService();
        service.RequestShutdown();

        // No active event stream so EventStreamFinished won't complete; it should time out.
        var task = service.WaitForShutdownAsync(TimeSpan.FromMilliseconds(10));
        await Assert.ThrowsAnyAsync<TimeoutException>(() => task);
    }

    // ── HandleWorkerMessageAsync dispatch ─────────────────────────────────────

    [Fact]
    public async Task HandleWorkerMessageAsync_UnknownMessageType_DoesNotThrow()
    {
        var service = CreateService();
        var message = new StreamingMessage { WorkerStatusRequest = new WorkerStatusRequest() };

        // Should handle without throwing
        await service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_RpcLog_DoesNotThrow()
    {
        var service = CreateService();
        var message = new StreamingMessage
        {
            RpcLog = new RpcLog
            {
                Level = RpcLog.Types.Level.Information,
                Message = "test log message",
                Category = "TestCategory"
            }
        };

        await service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_InvocationResponse_WithNoRequestId_DoesNotThrow()
    {
        var service = CreateService();
        var message = new StreamingMessage
        {
            // No RequestId set
            InvocationResponse = new InvocationResponse
            {
                InvocationId = "inv-1",
                Result = new StatusResult { Status = StatusResult.Types.Status.Success }
            }
        };

        await service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_InvocationResponse_Failure_DoesNotThrow()
    {
        var service = CreateService();
        var message = new StreamingMessage
        {
            RequestId = "req-1",
            InvocationResponse = new InvocationResponse
            {
                InvocationId = "inv-2",
                Result = new StatusResult
                {
                    Status = StatusResult.Types.Status.Failure,
                    Exception = new RpcException { Message = "function failed" }
                }
            }
        };

        // No pending request with this ID, so the CompleteRequest call will just log
        await service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_WorkerInitResponse_DoesNotThrow()
    {
        var service = CreateService();
        var message = new StreamingMessage
        {
            RequestId = "init-1",
            WorkerInitResponse = new WorkerInitResponse
            {
                Result = new StatusResult { Status = StatusResult.Types.Status.Success }
            }
        };

        await service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    [Fact]
    public async Task HandleWorkerMessageAsync_FunctionLoadResponse_DoesNotThrow()
    {
        var service = CreateService();
        var message = new StreamingMessage
        {
            RequestId = "load-1",
            FunctionLoadResponse = new FunctionLoadResponse
            {
                FunctionId = "fn-1",
                Result = new StatusResult { Status = StatusResult.Types.Status.Success }
            }
        };

        await service.HandleWorkerMessageAsync(message, CancellationToken.None);
    }

    // ── MapRpcLogLevel ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RpcLog.Types.Level.Trace, Microsoft.Extensions.Logging.LogLevel.Trace)]
    [InlineData(RpcLog.Types.Level.Debug, Microsoft.Extensions.Logging.LogLevel.Debug)]
    [InlineData(RpcLog.Types.Level.Information, Microsoft.Extensions.Logging.LogLevel.Information)]
    [InlineData(RpcLog.Types.Level.Warning, Microsoft.Extensions.Logging.LogLevel.Warning)]
    [InlineData(RpcLog.Types.Level.Error, Microsoft.Extensions.Logging.LogLevel.Error)]
    [InlineData(RpcLog.Types.Level.Critical, Microsoft.Extensions.Logging.LogLevel.Critical)]
    public void MapRpcLogLevel_KnownLevels_MapsCorrectly(
        RpcLog.Types.Level input, Microsoft.Extensions.Logging.LogLevel expected)
    {
        var result = GrpcHostService.MapRpcLogLevel(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void MapRpcLogLevel_UnknownLevel_ReturnsNone()
    {
        var result = GrpcHostService.MapRpcLogLevel((RpcLog.Types.Level)999);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.None, result);
    }

    // ── InvokeFunctionAsync – unknown function ────────────────────────────────

    [Fact]
    public async Task InvokeFunctionAsync_UnknownFunction_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var bindingData = new TriggerBindingData { InputData = [] };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InvokeFunctionAsync("NonExistentFunction", bindingData));
    }

    // ── SendMessageAsync – no connection ─────────────────────────────────────

    [Fact]
    public async Task SendMessageAsync_NoConnection_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var message = new StreamingMessage
        {
            RequestId = "req-1",
            WorkerInitRequest = new WorkerInitRequest { HostVersion = "1.0" }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendMessageAsync(message));
    }

    // ── GetCurrentEventStreamState ────────────────────────────────────────────

    [Fact]
    public void GetCurrentEventStreamState_ReturnsNonNullComponents()
    {
        var service = CreateService();
        var (shutdownCts, eventStreamFinished) = service.GetCurrentEventStreamState();

        Assert.NotNull(shutdownCts);
        Assert.NotNull(eventStreamFinished);
        Assert.False(shutdownCts.IsCancellationRequested);
    }

    // ── GetFunctions ──────────────────────────────────────────────────────────

    [Fact]
    public void GetFunctions_Initially_ReturnsEmpty()
    {
        var service = CreateService();
        Assert.Empty(service.GetFunctions());
    }
}
