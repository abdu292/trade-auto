using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Brain.Web.Filters;

public sealed class RequestLoggingFilter(ILogger<RequestLoggingFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        logger.LogInformation("Endpoint {Path} called", context.HttpContext.Request.Path.Value);
        return await next(context);
    }
}
