using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TestProject;

public sealed class CorrelationMiddleware : IFunctionsWorkerMiddleware
{
    public const string HeaderName = "x-correlation-id";
    public const string ItemKey = "CorrelationId";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // DIAGNOSTIC: check feature and items state at entry
        var logger = context.InstanceServices.GetService<ILogger<CorrelationMiddleware>>();
        var hasHttpRequestDataFeature = context.Features.Get<Microsoft.Azure.Functions.Worker.Http.IHttpRequestDataFeature>() != null;
        var inputBindings = context.FunctionDefinition?.InputBindings
            .Select(kvp => $"{kvp.Key}={kvp.Value.Type}")
            .ToArray() ?? [];
        logger?.LogWarning(
            "CORR ENTRY: invId={InvId}, func={Func}, hasHttpRequestDataFeature={HF}, itemCount={IC}, inputBindings=[{IB}]",
            context.InvocationId,
            context.FunctionDefinition?.Name,
            hasHttpRequestDataFeature,
            context.Items.Count,
            string.Join(", ", inputBindings));

        var requestData = await context.GetHttpRequestDataAsync();
        if (requestData?.Headers.TryGetValues(HeaderName, out var values) == true)
        {
            var correlationId = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(correlationId))
                context.Items[ItemKey] = correlationId;
        }

        await next(context);
    }
}
