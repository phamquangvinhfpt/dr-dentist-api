using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FSH.WebApi.Infrastructure.Payments;
public static class Startup
{
    public static IServiceCollection AddPayment(this IServiceCollection services, IConfiguration config) =>
        services.Configure<PaymentSettings>(config.GetSection(nameof(PaymentSettings)));
}
