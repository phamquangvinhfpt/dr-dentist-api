using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FSH.WebApi.Infrastructure.SpeedSMS;
public static class Startup
{
    public static IServiceCollection AddSpeedSMS(this IServiceCollection services, IConfiguration config) =>
        services.Configure<SpeedSMSSettings>(config.GetSection(nameof(SpeedSMSSettings)));
}
