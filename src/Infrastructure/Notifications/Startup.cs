using FSH.WebApi.Infrastructure.Chat;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FSH.WebApi.Infrastructure.Notifications;

internal static class Startup
{
    internal static IServiceCollection AddNotificationsAndChat(this IServiceCollection services, IConfiguration config)
    {
        ILogger logger = Log.ForContext(typeof(Startup));

        var signalRSettings = config.GetSection(nameof(SignalRSettings)).Get<SignalRSettings>();

        if (!signalRSettings.UseBackplane)
        {
            services.AddSingleton<PresenceTracker>();
            services.AddSignalR(options =>
            {
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            services.AddSingleton<PresenceTracker>();
            var backplaneSettings = config.GetSection("SignalRSettings:Backplane").Get<SignalRSettings.Backplane>();
            if (backplaneSettings is null) throw new InvalidOperationException("Backplane enabled, but no backplane settings in config.");
            switch (backplaneSettings.Provider)
            {
                case "redis":
                    if (backplaneSettings.StringConnection is null) throw new InvalidOperationException("Redis backplane provider: No connectionString configured.");
                    services.AddSignalR(options =>
                    {
                        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                        options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                    }).AddStackExchangeRedis(backplaneSettings.StringConnection, options =>
                    {
                        options.Configuration.AbortOnConnectFail = false;
                    });
                    break;

                default:
                    throw new InvalidOperationException($"SignalR backplane Provider {backplaneSettings.Provider} is not supported.");
            }

            logger.Information($"SignalR Backplane Current Provider: {backplaneSettings.Provider}.");
        }

        return services;
    }

    internal static IEndpointRouteBuilder MapNotificationsAndChat(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<NotificationHub>("/notifications", options =>
        {
            options.CloseOnAuthenticationExpiration = true;
        });

        return endpoints;
    }
}