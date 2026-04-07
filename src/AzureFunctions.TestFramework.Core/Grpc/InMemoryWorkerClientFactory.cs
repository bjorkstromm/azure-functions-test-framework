using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AzureFunctions.TestFramework.Core.Grpc;

/// <summary>
/// Replaces the Worker SDK's internal <c>IWorkerClientFactory</c> via
/// <see cref="DispatchProxy"/> so all gRPC traffic between the Worker SDK and the
/// <see cref="GrpcHostService"/> is routed through a <see cref="System.Net.Http.HttpMessageHandler"/>
/// obtained from <c>TestServer.CreateHandler()</c> — no TCP socket is opened.
/// </summary>
internal sealed class InMemoryGrpcClientFactory : DispatchProxy
{
    private HttpMessageHandler _handler = null!;
    private Type _workerClientInterface = null!;
    private Type _functionRpcClientType = null!;
    private MethodInfo _eventStreamMethod = null!;
    private MethodInfo _processMessageAsyncMethod = null!;

    internal void Initialize(
        HttpMessageHandler handler,
        Type workerClientInterface,
        Type functionRpcClientType,
        MethodInfo eventStreamMethod,
        MethodInfo processMessageAsync)
    {
        _handler = handler;
        _workerClientInterface = workerClientInterface;
        _functionRpcClientType = functionRpcClientType;
        _eventStreamMethod = eventStreamMethod;
        _processMessageAsyncMethod = processMessageAsync;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "CreateClient")
        {
            return CreateWorkerClient(args![0]!);
        }

        throw new NotSupportedException(
            $"Unexpected method call on IWorkerClientFactory proxy: {targetMethod?.Name}");
    }

    private object CreateWorkerClient(object processor)
    {
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _handler,
            DisposeHttpClient = false
        });

        // FunctionRpc.FunctionRpcClient has a public ctor(ChannelBase channel).
        // FunctionRpc (outer class) is internal, so we must use Activator.
        var rpcClient = Activator.CreateInstance(_functionRpcClientType, new object[] { channel })!;

        var createProxyMethod = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "Create" && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(_workerClientInterface, typeof(InMemoryGrpcWorkerClient));

        var clientProxy = (InMemoryGrpcWorkerClient)createProxyMethod.Invoke(null, null)!;
        clientProxy.Initialize(rpcClient, processor, _eventStreamMethod, _processMessageAsyncMethod);
        return clientProxy;
    }

    /// <summary>
    /// Registers an in-memory <c>IWorkerClientFactory</c> that routes gRPC traffic through
    /// the provided <paramref name="handler"/> (obtained from <c>TestServer.CreateHandler()</c>).
    /// Must be called <b>after</b> the Worker SDK has registered its default factory so that
    /// our <c>AddSingleton</c> registration (last-wins) takes precedence.
    /// </summary>
    /// <returns><see langword="true"/> if registration succeeded.</returns>
    internal static bool TryRegister(IServiceCollection services, HttpMessageHandler handler, ILogger logger)
    {
        try
        {
            var workerGrpcAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Microsoft.Azure.Functions.Worker.Grpc");

            if (workerGrpcAsm == null)
            {
                logger.LogWarning(
                    "Microsoft.Azure.Functions.Worker.Grpc assembly not yet loaded; in-memory gRPC not registered");
                return false;
            }

            var factoryInterface = workerGrpcAsm.GetType(
                "Microsoft.Azure.Functions.Worker.Grpc.IWorkerClientFactory");
            var clientInterface = workerGrpcAsm.GetType(
                "Microsoft.Azure.Functions.Worker.Grpc.IWorkerClient");
            var processorInterface = workerGrpcAsm.GetType(
                "Microsoft.Azure.Functions.Worker.Grpc.IMessageProcessor");
            var functionRpcType = workerGrpcAsm.GetType(
                "Microsoft.Azure.Functions.Worker.Grpc.Messages.FunctionRpc");
            var functionRpcClientType = functionRpcType?.GetNestedType("FunctionRpcClient");

            if (factoryInterface == null || clientInterface == null
                || processorInterface == null || functionRpcClientType == null)
            {
                logger.LogWarning(
                    "Required internal types not found in Worker.Grpc assembly; in-memory gRPC not registered");
                return false;
            }

            // EventStream(Metadata headers, DateTime? deadline, CancellationToken cancellationToken)
            var eventStreamMethod = functionRpcClientType.GetMethods()
                .FirstOrDefault(m => m.Name == "EventStream" && m.GetParameters().Length == 3);

            var processMessageAsync = processorInterface.GetMethod("ProcessMessageAsync");

            if (eventStreamMethod == null || processMessageAsync == null)
            {
                logger.LogWarning("Required gRPC methods not found; in-memory gRPC not registered");
                return false;
            }

            var createFactoryProxy = typeof(DispatchProxy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Create" && m.GetGenericArguments().Length == 2)
                .MakeGenericMethod(factoryInterface, typeof(InMemoryGrpcClientFactory));

            var factoryProxy = (InMemoryGrpcClientFactory)createFactoryProxy.Invoke(null, null)!;
            factoryProxy.Initialize(
                handler,
                clientInterface,
                functionRpcClientType,
                eventStreamMethod,
                processMessageAsync);

            // AddSingleton (not TryAdd) so we override the SDK's TryAddSingleton registration.
            services.AddSingleton(factoryInterface, factoryProxy);

            logger.LogDebug("In-memory IWorkerClientFactory registered — gRPC TCP bypassed");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to register in-memory IWorkerClientFactory; falling back to TCP gRPC");
            return false;
        }
    }
}

/// <summary>
/// Implements the Worker SDK's internal <c>IWorkerClient</c> via <see cref="DispatchProxy"/>.
/// Drives the bidirectional gRPC <c>EventStream</c> over the in-memory
/// <see cref="System.Net.Http.HttpMessageHandler"/> without opening a TCP socket.
/// </summary>
internal sealed class InMemoryGrpcWorkerClient : DispatchProxy
{
    private object _rpcClient = null!;
    private object _processor = null!;
    private MethodInfo _eventStreamMethod = null!;
    private MethodInfo _processMessageAsync = null!;
    private object? _requestStream;
    private MethodInfo? _requestStreamWriteAsync;
    private MethodInfo? _requestStreamComplete;

    internal void Initialize(
        object rpcClient,
        object processor,
        MethodInfo eventStreamMethod,
        MethodInfo processMessageAsync)
    {
        _rpcClient = rpcClient;
        _processor = processor;
        _eventStreamMethod = eventStreamMethod;
        _processMessageAsync = processMessageAsync;
    }

    /// <inheritdoc/>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "StartAsync" => StartAsync((CancellationToken)args![0]!),
            "SendMessageAsync" => SendMessageAsync(args![0]!),
            _ => throw new NotSupportedException(
                $"Unexpected method call on IWorkerClient proxy: {targetMethod?.Name}")
        };
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        // Call EventStream(Metadata.Empty, deadline: null, cancellationToken).
        var call = _eventStreamMethod.Invoke(
            _rpcClient,
            new object?[] { Metadata.Empty, null, cancellationToken })!;

        var callType = call.GetType();
        _requestStream = callType.GetProperty("RequestStream")!.GetValue(call)!;
        var responseStream = callType.GetProperty("ResponseStream")!.GetValue(call)!;

        // Discover WriteAsync on IClientStreamWriter<StreamingMessage>.
        var msgType = _processMessageAsync.GetParameters()[0].ParameterType;
        _requestStreamWriteAsync = _requestStream.GetType().GetInterfaces()
            .SelectMany(i => i.GetMethods())
            .FirstOrDefault(m => m.Name == "WriteAsync"
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == msgType);

        _requestStreamComplete = _requestStream.GetType().GetInterfaces()
            .SelectMany(i => i.GetMethods())
            .FirstOrDefault(m => m.Name == "CompleteAsync" && m.GetParameters().Length == 0);

        // Send StartStream as the first message so GrpcHostService sends WorkerInitRequest back.
        await SendStartStreamAsync(msgType);

        // Pump response messages in the background.
        _ = Task.Run(() => ReadResponseStreamAsync(responseStream, cancellationToken), CancellationToken.None);
    }

    private async Task SendStartStreamAsync(Type streamingMessageType)
    {
        if (_requestStreamWriteAsync == null) return;

        var msgAsm = streamingMessageType.Assembly;
        var startStreamType = msgAsm.GetType(
            "Microsoft.Azure.Functions.Worker.Grpc.Messages.StartStream");
        if (startStreamType == null) return;

        var startStreamMsg = Activator.CreateInstance(startStreamType)!;
        startStreamType.GetProperty("WorkerId")?.SetValue(startStreamMsg, Guid.NewGuid().ToString());

        var streamingMsg = Activator.CreateInstance(streamingMessageType)!;
        streamingMessageType.GetProperty("StartStream")?.SetValue(streamingMsg, startStreamMsg);

        await (Task)_requestStreamWriteAsync.Invoke(_requestStream!, new[] { streamingMsg })!;
    }

    private async Task ReadResponseStreamAsync(object responseStream, CancellationToken cancellationToken)
    {
        var interfaces = responseStream.GetType().GetInterfaces();
        var moveNextMethod = interfaces
            .SelectMany(i => i.GetMethods())
            .FirstOrDefault(m => m.Name == "MoveNext" && m.GetParameters().Length == 1)
            ?? responseStream.GetType().GetMethod("MoveNext");
        var currentProperty = interfaces
            .SelectMany(i => i.GetProperties())
            .FirstOrDefault(p => p.Name == "Current")
            ?? responseStream.GetType().GetProperty("Current");

        if (moveNextMethod == null || currentProperty == null) return;

        try
        {
            while (await (Task<bool>)moveNextMethod.Invoke(responseStream, new object[] { cancellationToken })!)
            {
                var msg = currentProperty.GetValue(responseStream)!;
                await (Task)_processMessageAsync.Invoke(_processor, new[] { msg })!;
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        catch (Exception) { /* stream ended */ }
    }

    private ValueTask SendMessageAsync(object message)
    {
        if (_requestStreamWriteAsync == null || _requestStream == null)
            return ValueTask.CompletedTask;

        return new ValueTask((Task)_requestStreamWriteAsync.Invoke(_requestStream, new[] { message })!);
    }
}
