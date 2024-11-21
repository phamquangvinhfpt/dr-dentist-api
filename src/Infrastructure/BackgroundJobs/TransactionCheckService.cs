using FSH.WebApi.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.BackgroundJobs;

public class TransactionCheckService : BackgroundService
{
    private readonly ILogger<TransactionCheckService> _logger;
    private string _apiUrl;
    private readonly IHostEnvironment _env;

    public TransactionCheckService(ILogger<TransactionCheckService> logger, IConfiguration config, IHostEnvironment env)
    {
        _logger = logger;
        var settings = config.GetSection(nameof(PaymentSettings)).Get<PaymentSettings>();
        _apiUrl = $"{settings.SyncJobURL}/api/v1/payment/check-new-transactions";
        _env = env;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_env.IsProduction())
                {
                    _logger.LogInformation("Current environment: Production");
                    _apiUrl = "https://api.drdentist.me/api/v1/payment/check-new-transactions";
                    TransactionsUtils.CallAPIChecking(_apiUrl);
                    _logger.LogInformation("Checked transactions at: {time}", DateTimeOffset.Now);
                }
                else if (_env.IsDevelopment())
                {
                    _logger.LogInformation("Current environment: Development");
                    //TransactionsUtils.CallAPIChecking(_apiUrl);
                    _logger.LogInformation("Checked transactions at: {time}", DateTimeOffset.Now);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking transactions.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}