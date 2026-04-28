using AzureFunctions.TestFramework.Core.Grpc;
using Microsoft.Azure.WebJobs.Script.Grpc.Messages;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AzureFunctions.TestFramework.Tests.Core;

/// <summary>
/// Unit tests for <see cref="GrpcHostService.MapRpcLogLevel"/>.
/// </summary>
public class GrpcHostServiceRpcLogTests
{
    [Theory]
    [InlineData(RpcLog.Types.Level.Trace, LogLevel.Trace)]
    [InlineData(RpcLog.Types.Level.Debug, LogLevel.Debug)]
    [InlineData(RpcLog.Types.Level.Information, LogLevel.Information)]
    [InlineData(RpcLog.Types.Level.Warning, LogLevel.Warning)]
    [InlineData(RpcLog.Types.Level.Error, LogLevel.Error)]
    [InlineData(RpcLog.Types.Level.Critical, LogLevel.Critical)]
    public void MapRpcLogLevel_KnownLevel_ReturnsCorrectLogLevel(
        RpcLog.Types.Level rpcLevel,
        LogLevel expectedLogLevel)
    {
        var result = GrpcHostService.MapRpcLogLevel(rpcLevel);
        Assert.Equal(expectedLogLevel, result);
    }

    [Fact]
    public void MapRpcLogLevel_UnknownLevel_ReturnsNone()
    {
        var result = GrpcHostService.MapRpcLogLevel((RpcLog.Types.Level)999);
        Assert.Equal(LogLevel.None, result);
    }

    [Theory]
    [InlineData(RpcLog.Types.Level.Trace)]
    [InlineData(RpcLog.Types.Level.Debug)]
    [InlineData(RpcLog.Types.Level.Information)]
    [InlineData(RpcLog.Types.Level.Warning)]
    [InlineData(RpcLog.Types.Level.Error)]
    [InlineData(RpcLog.Types.Level.Critical)]
    public void MapRpcLogLevel_AllKnownLevels_NeverReturnsNone(RpcLog.Types.Level rpcLevel)
    {
        var result = GrpcHostService.MapRpcLogLevel(rpcLevel);
        Assert.NotEqual(LogLevel.None, result);
    }
}
