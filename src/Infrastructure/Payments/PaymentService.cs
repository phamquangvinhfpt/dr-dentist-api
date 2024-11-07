using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
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
    private readonly IAppointmentService _appointmentService;
    private readonly ICacheService _cacheService;

    public PaymentService(ILogger<PaymentService> logger, ApplicationDbContext context, INotificationService notificationService, FSHTenantInfo tenantInfo, IAppointmentService appointmentService, ICacheService cacheService)
    {
        _logger = logger;
        _context = context;
        _notificationService = notificationService;
        _tenantInfo = tenantInfo;
        _appointmentService = appointmentService;
        _cacheService = cacheService;
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
                var check_context = await _context.PatientProfiles.AnyAsync(p => p.PatientCode == transaction.Description);
                if (check_context)
                {
                    var deposit_info = await _cacheService.GetAsync<AppointmentDepositRequest>(transaction.Description, cancellationToken);
                    if (deposit_info != null)
                    {
                        if(deposit_info.DepositAmount == double.Parse(transaction.Amount))
                        {
                            await _appointmentService.VerifyAndFinishBooking(deposit_info, cancellationToken);
                            await _cacheService.RemoveAsync(transaction.Description);
                        }
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while saving transactions", ex.Message);
        }
    }
}