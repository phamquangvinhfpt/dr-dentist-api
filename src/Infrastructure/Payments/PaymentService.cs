using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Payments;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace FSH.WebApi.Infrastructure.Payments;

public class PaymentService : IPaymentService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly FSHTenantInfo _tenantInfo;
    private readonly IAppointmentService _appointmentService;
    private readonly ICacheService _cacheService;
    private readonly IOptions<PaymentSettings> _settings;

    public PaymentService(ILogger<PaymentService> logger, ApplicationDbContext context, INotificationService notificationService,
        FSHTenantInfo tenantInfo, IAppointmentService appointmentService, ICacheService cacheService, IOptions<PaymentSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _notificationService = notificationService;
        _tenantInfo = tenantInfo;
        _appointmentService = appointmentService;
        _cacheService = cacheService;
        _settings = settings;
    }

    public async Task CheckNewTransactions(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing new transactions...");
        List<Transaction> newTransactions = await GetNewTransaction(cancellationToken);
        if (newTransactions.Count == 0)
        {
            _logger.LogInformation("No new transactions.");
            return;
        }

        foreach (var transaction in newTransactions)
        {
            var check_context = await _context.PatientProfiles.AnyAsync(p => p.PatientCode == transaction.Description);
            if (check_context)
            {
                var deposit_info = await _cacheService.GetAsync<AppointmentDepositRequest>(transaction.Description, cancellationToken);
                if (deposit_info != null)
                {
                    if(deposit_info.DepositAmount == decimal.ToDouble(transaction.Amount))
                    {
                        await _appointmentService.VerifyAndFinishBooking(deposit_info, cancellationToken);
                        await _cacheService.RemoveAsync(transaction.Description);
                    }
                }
            }

            await _context.AddAsync(transaction);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Added {count} new transactions.", newTransactions.Count);
    }

    private async Task<List<Transaction>> SyncTransactions()
    {
        HttpClient client = new HttpClient();
        var response = await client.GetAsync(_settings.Value.TransactionsURL);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadFromJsonAsync<TransactionAPIResponse>();
            if (content.Status && content.Transactions.Count > 0)
            {

                return content.Transactions.Select(t => new Transaction
                {
                    TransactionID = t.TransactionID,
                    Amount = t.Amount,
                    Description = t.Description,
                    TransactionDate = DateOnly.ParseExact(t.TransactionDate, "dd/MM/yyyy"),
                    Type = t.Type == "IN" ? TransactionType.IN : TransactionType.OUT,
                    IsSuccess = true
                }).Where(t => t.Type.Equals(TransactionType.IN)).ToList();
            }
        }
        else
        {
            _logger.LogError("Failed to get new transactions from banking.");
        }

        return new List<Transaction>();
    }

    private async Task<List<Transaction>> GetNewTransaction(CancellationToken cancellationToken)
    {
        List<Transaction> syncTransactions = await SyncTransactions();
        if (syncTransactions.Count == 0) return syncTransactions;
        List<Transaction> todayTransaction = await _context.Transactions.ToListAsync(cancellationToken);
        return syncTransactions.Where(t => !todayTransaction.Any(tt => tt.TransactionID == t.TransactionID)).ToList();
    }
}