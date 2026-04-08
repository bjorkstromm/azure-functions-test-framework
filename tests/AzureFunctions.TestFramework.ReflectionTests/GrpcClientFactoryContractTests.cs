using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace AzureFunctions.TestFramework.ReflectionTests;

/// <summary>
/// SDK contract tests for <c>InMemoryGrpcClientFactory</c> and <c>InMemoryGrpcWorkerClient</c>.
///
/// <para>These tests verify that the internal Worker gRPC types and members that the
/// in-memory gRPC interception depends on via reflection are still present in the
/// <c>Microsoft.Azure.Functions.Worker.Grpc</c> assembly.</para>
///
/// <para>See <c>docs/Reflection.md</c> § 2 for full context.</para>
/// </summary>
public class GrpcClientFactoryContractTests
{
    private const string WorkerGrpcAssemblyName = "Microsoft.Azure.Functions.Worker.Grpc";
    private const string WorkerClientFactoryName = "Microsoft.Azure.Functions.Worker.Grpc.IWorkerClientFactory";
    private const string WorkerClientName = "Microsoft.Azure.Functions.Worker.Grpc.IWorkerClient";
    private const string MessageProcessorName = "Microsoft.Azure.Functions.Worker.Grpc.IMessageProcessor";
    private const string FunctionRpcTypeName = "Microsoft.Azure.Functions.Worker.Grpc.Messages.FunctionRpc";
    private const string StartStreamTypeName = "Microsoft.Azure.Functions.Worker.Grpc.Messages.StartStream";
    private const string StreamingMessageTypeName = "Microsoft.Azure.Functions.Worker.Grpc.Messages.StreamingMessage";

    private static Assembly WorkerGrpcAssembly =>
        AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == WorkerGrpcAssemblyName);

    // Force the gRPC assembly to load. All types in that assembly are internal so we
    // cannot reference them at compile time. We trigger the load by using the public
    // WorkerOptions type (Worker.Core), whose package has Worker.Grpc as a dependency.
    static GrpcClientFactoryContractTests()
    {
        // Trigger loading Worker.Core (and its transitive dependency Worker.Grpc).
        _ = typeof(WorkerOptions).Assembly.FullName;
        // Load Worker.Grpc explicitly if not yet loaded (resolves from the NuGet package path).
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == WorkerGrpcAssemblyName))
        {
            // The DLL is always in the same directory as WorkerOptions because the two packages
            // are always used together and their outputs land in the same bin folder.
            var workerDir = Path.GetDirectoryName(typeof(WorkerOptions).Assembly.Location)!;
            var grpcPath = Path.Combine(workerDir, WorkerGrpcAssemblyName + ".dll");
            if (File.Exists(grpcPath))
                Assembly.LoadFrom(grpcPath);
        }
    }

    [Fact]
    public void IWorkerClientFactory_ExistsInWorkerGrpcAssembly()
    {
        var type = WorkerGrpcAssembly.GetType(WorkerClientFactoryName);
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IWorkerClientFactory_HasCreateClientMethod()
    {
        var factoryType = WorkerGrpcAssembly.GetType(WorkerClientFactoryName);
        Assert.NotNull(factoryType);

        var createClient = factoryType.GetMethods()
            .FirstOrDefault(m => m.Name == "CreateClient");

        Assert.NotNull(createClient);
        Assert.Single(createClient.GetParameters());
    }

    [Fact]
    public void IWorkerClient_ExistsInWorkerGrpcAssembly()
    {
        var type = WorkerGrpcAssembly.GetType(WorkerClientName);
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IWorkerClient_HasStartAsyncAndSendMessageAsync()
    {
        var clientType = WorkerGrpcAssembly.GetType(WorkerClientName);
        Assert.NotNull(clientType);

        var startAsync = clientType.GetMethods()
            .FirstOrDefault(m => m.Name == "StartAsync");
        Assert.NotNull(startAsync);
        Assert.Single(startAsync.GetParameters());
        Assert.Equal(typeof(System.Threading.CancellationToken), startAsync.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(Task), startAsync.ReturnType);

        var sendMessageAsync = clientType.GetMethods()
            .FirstOrDefault(m => m.Name == "SendMessageAsync");
        Assert.NotNull(sendMessageAsync);
    }

    [Fact]
    public void IMessageProcessor_ExistsInWorkerGrpcAssembly()
    {
        var type = WorkerGrpcAssembly.GetType(MessageProcessorName);
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IMessageProcessor_HasProcessMessageAsync()
    {
        var processorType = WorkerGrpcAssembly.GetType(MessageProcessorName);
        Assert.NotNull(processorType);

        var method = processorType.GetMethod("ProcessMessageAsync");
        Assert.NotNull(method);
        Assert.Single(method.GetParameters());
    }

    [Fact]
    public void FunctionRpc_FunctionRpcClient_NestedTypeExists()
    {
        var functionRpcType = WorkerGrpcAssembly.GetType(FunctionRpcTypeName);
        Assert.NotNull(functionRpcType);

        var clientType = functionRpcType.GetNestedType("FunctionRpcClient");
        Assert.NotNull(clientType);
    }

    [Fact]
    public void FunctionRpcClient_HasEventStreamMethodWithThreeParameters()
    {
        var functionRpcType = WorkerGrpcAssembly.GetType(FunctionRpcTypeName);
        Assert.NotNull(functionRpcType);

        var clientType = functionRpcType.GetNestedType("FunctionRpcClient");
        Assert.NotNull(clientType);

        // EventStream(Metadata headers, DateTime? deadline, CancellationToken cancellationToken)
        var eventStreamMethod = clientType.GetMethods()
            .FirstOrDefault(m => m.Name == "EventStream" && m.GetParameters().Length == 3);

        Assert.NotNull(eventStreamMethod);

        var parameters = eventStreamMethod.GetParameters();
        Assert.Equal("headers", parameters[0].Name);
        Assert.Equal("deadline", parameters[1].Name);
        Assert.Equal("cancellationToken", parameters[2].Name);
    }

    [Fact]
    public void FunctionRpcClient_HasPublicChannelConstructor()
    {
        var functionRpcType = WorkerGrpcAssembly.GetType(FunctionRpcTypeName);
        Assert.NotNull(functionRpcType);

        var clientType = functionRpcType.GetNestedType("FunctionRpcClient");
        Assert.NotNull(clientType);

        // Activator.CreateInstance(clientType, channel) requires a (ChannelBase) constructor
        var ctor = clientType.GetConstructors()
            .FirstOrDefault(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 1 &&
                       typeof(Grpc.Core.ChannelBase).IsAssignableFrom(ps[0].ParameterType);
            });

        Assert.NotNull(ctor);
    }

    [Fact]
    public void EventStreamCall_HasRequestStreamAndResponseStreamProperties()
    {
        var functionRpcType = WorkerGrpcAssembly.GetType(FunctionRpcTypeName);
        Assert.NotNull(functionRpcType);

        var clientType = functionRpcType.GetNestedType("FunctionRpcClient");
        Assert.NotNull(clientType);

        var eventStreamMethod = clientType.GetMethods()
            .First(m => m.Name == "EventStream" && m.GetParameters().Length == 3);

        var returnType = eventStreamMethod.ReturnType;
        Assert.NotNull(returnType.GetProperty("RequestStream"));
        Assert.NotNull(returnType.GetProperty("ResponseStream"));
    }

    [Fact]
    public void StartStream_TypeExistsWithWorkerIdProperty()
    {
        var startStreamType = WorkerGrpcAssembly.GetType(StartStreamTypeName);

        Assert.NotNull(startStreamType);

        var workerIdProp = startStreamType.GetProperty("WorkerId");
        Assert.NotNull(workerIdProp);
        Assert.Equal(typeof(string), workerIdProp.PropertyType);
    }

    [Fact]
    public void StreamingMessage_TypeExistsWithStartStreamProperty()
    {
        var streamingMsgType = WorkerGrpcAssembly.GetType(StreamingMessageTypeName);
        Assert.NotNull(streamingMsgType);

        var prop = streamingMsgType.GetProperty("StartStream");
        Assert.NotNull(prop);

        var startStreamType = WorkerGrpcAssembly.GetType(StartStreamTypeName);
        Assert.NotNull(startStreamType);
        Assert.Equal(startStreamType, prop.PropertyType);
    }

    [Fact]
    public void DispatchProxy_Create_CanBeInvokedWithWorkerClientInterface()
    {
        var clientInterface = WorkerGrpcAssembly.GetType(WorkerClientName);
        Assert.NotNull(clientInterface);

        var createMethod = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "Create" && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(clientInterface, typeof(NoOpWorkerClientProxy));

        var proxy = createMethod.Invoke(null, null);
        Assert.NotNull(proxy);
        Assert.IsAssignableFrom(clientInterface, proxy);
    }

    [Fact]
    public void DispatchProxy_Create_CanBeInvokedWithWorkerClientFactoryInterface()
    {
        var factoryInterface = WorkerGrpcAssembly.GetType(WorkerClientFactoryName);
        Assert.NotNull(factoryInterface);

        var createMethod = typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "Create" && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(factoryInterface, typeof(NoOpClientFactoryProxy));

        var proxy = createMethod.Invoke(null, null);
        Assert.NotNull(proxy);
        Assert.IsAssignableFrom(factoryInterface, proxy);
    }

    /// <summary>Minimal DispatchProxy subclasses used to validate proxy creation.</summary>
    public class NoOpWorkerClientProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException("Should not be invoked in contract tests.");
    }

    public class NoOpClientFactoryProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException("Should not be invoked in contract tests.");
    }
}
