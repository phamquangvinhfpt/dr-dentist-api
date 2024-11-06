using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.Payments;

public class PaymentService : IPaymentService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly FSHTenantInfo _tenantInfo;

    public PaymentService(ILogger<PaymentService> logger, ApplicationDbContext context, INotificationService notificationService, FSHTenantInfo tenantInfo)
    {
        _logger = logger;
        _context = context;
        _notificationService = notificationService;
        _tenantInfo = tenantInfo;
    }

    public async Task SaveTransactions(List<TransactionInfo> data, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving transactions...");
        if (data == null || data.Count == 0)
        {
            _logger.LogWarning("No transactions to save");
            return;
        }

        try
        {
            foreach (var transaction in data)
            {
                var existingTransaction = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transaction.TransactionId);
                if (existingTransaction != null)
                {
                    _logger.LogWarning("Transaction already exists: {0}", transaction.TransactionId);
                    continue;
                }

                await _context.Transactions.AddAsync(transaction);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving transactions", ex.Message);
        }
    }
}