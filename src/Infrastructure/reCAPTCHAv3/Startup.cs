using FSH.WebApi.Infrastructure.Common.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FSH.WebApi.Infrastructure.reCAPTCHAv3;

internal static class Startup
{
    internal static IServiceCollection AddReCaptchav3(this IServiceCollection services, IConfiguration config) =>
        services.Configure<reCAPTCHAv3Settings>(config.GetSection(nameof(reCAPTCHAv3Settings)));
}