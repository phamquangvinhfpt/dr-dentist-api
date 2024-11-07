using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class TenantIdMiddleware : IMiddleware
{
    private readonly ILogger<TenantIdMiddleware> _logger;

    public TenantIdMiddleware(ILogger<TenantIdMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/api/v1/webhook"))
        {
            context.Request.Headers.Add("tenant", "root");
        } else if (context.Request.Path.StartsWithSegments("/api/v1/payment/check-new-transactions"))
        {
            if (!context.Request.Headers.ContainsKey("tenant"))
            {
                context.Request.Headers.Add("tenant", "root");
            }
        }

        await next(context);
    }
}