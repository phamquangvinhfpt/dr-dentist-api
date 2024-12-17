using FSH.WebApi.Infrastructure.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FSH.WebApi.Infrastructure.Middleware;

internal static class Startup
{
    internal static IServiceCollection AddExceptionMiddleware(this IServiceCollection services) =>
        services.AddScoped<ExceptionMiddleware>();

    internal static IServiceCollection AddTenantIdMiddleware(this IServiceCollection services) =>
        services.AddScoped<TenantIdMiddleware>();

    internal static IServiceCollection AddQueueRequest(this IServiceCollection services)
    {
        services.AddScoped<QueueMiddleware>();
        services.AddScoped<RequestQueue>();
        return services;
    }
    internal static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionMiddleware>();

    internal static IApplicationBuilder UseTenantIdMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantIdMiddleware>();

    internal static IApplicationBuilder UseQueueRequestMiddleware(this IApplicationBuilder app) =>
        app.UseMiddleware<QueueMiddleware>();

    internal static IServiceCollection AddRequestLogging(this IServiceCollection services, IConfiguration config)
    {
        if (GetMiddlewareSettings(config).EnableHttpsLogging)
        {
            services.AddSingleton<RequestLoggingMiddleware>();
            services.AddScoped<ResponseLoggingMiddleware>();
        }

        return services;
    }

    internal static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app, IConfiguration config)
    {
        if (GetMiddlewareSettings(config).EnableHttpsLogging)
        {
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseMiddleware<ResponseLoggingMiddleware>();
        }

        return app;
    }

    private static MiddlewareSettings GetMiddlewareSettings(IConfiguration config) =>
        config.GetSection(nameof(MiddlewareSettings)).Get<MiddlewareSettings>()!;
}