using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Sample.FunctionApp.Worker;

public sealed class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    public const string HeaderName = "x-correlation-id";
    public const string ItemKey = "CorrelationId";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();
        if (requestData?.Headers.TryGetValues(HeaderName, out var values) == true)
        {
            var correlationId = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                context.Items[ItemKey] = correlationId;
            }
        }

        await next(context);
    }
}
