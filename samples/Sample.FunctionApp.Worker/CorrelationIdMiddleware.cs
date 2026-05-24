using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Sample.FunctionApp.Worker;

/// <summary>
/// Represents this type.
/// </summary>
public sealed class CorrelationIdMiddleware : IFunctionsWorkerMiddleware
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public const string HeaderName = "x-correlation-id";
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public const string ItemKey = "CorrelationId";

    /// <summary>
    /// Executes this operation.
    /// </summary>
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
