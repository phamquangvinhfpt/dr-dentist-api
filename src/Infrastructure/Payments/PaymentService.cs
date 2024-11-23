using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Office2010.Excel;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Payments;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Http;
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
    private readonly IAppointmentService _appointmentService;
    private readonly ICacheService _cacheService;
    private readonly IOptions<PaymentSettings> _settings;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    public PaymentService(UserManager<ApplicationUser> userManager, ILogger<PaymentService> logger, ApplicationDbContext context, IAppointmentService appointmentService, ICacheService cacheService, IOptions<PaymentSettings> settings, ICurrentUser currentUserService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _appointmentService = appointmentService;
        _cacheService = cacheService;
        _settings = settings;
        _currentUserService = currentUserService;
        _userManager = userManager;
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
            await _context.AddAsync(transaction);
            await _context.SaveChangesAsync();
            var check_context = await _context.PatientProfiles.FirstOrDefaultAsync(p => transaction.Description.Contains(p.PatientCode), cancellationToken);
            if (check_context != null)
            {
                var info = await _cacheService.GetAsync<PayAppointmentRequest>(check_context.PatientCode, cancellationToken);
                if (info != null && info.Amount == decimal.ToDouble(transaction.Amount))
                {
                    if (info.IsVerify)
                    {
                        await _appointmentService.VerifyAndFinishBooking(info, cancellationToken);
                    }
                    else
                    {
                        await _appointmentService.DoPaymentForAppointment(info, cancellationToken);
                    }
                    await _cacheService.RemoveAsync(transaction.Description, cancellationToken);
                }
            }
        }

        _logger.LogInformation("Added {count} new transactions.", newTransactions.Count);
    }

    public async Task CheckTransactionsAsync(CancellationToken cancellationToken)
    {
        List<Transaction> transactions = await _context.Transactions.ToListAsync(cancellationToken);
        if (transactions.Count == 0)
        {
            _logger.LogInformation("No transactions.");
        }

        foreach (var transaction in transactions)
        {
            var check_context = await _context.PatientProfiles.FirstOrDefaultAsync(p => transaction.Description.Contains(p.PatientCode), cancellationToken);
            if (check_context != null)
            {
                var info = await _cacheService.GetAsync<PayAppointmentRequest>(check_context.PatientCode, cancellationToken);
                if (info != null && info.Amount == decimal.ToDouble(transaction.Amount))
                {
                    if (info.IsVerify)
                    {
                        await _appointmentService.VerifyAndFinishBooking(info, cancellationToken);
                    }
                    else
                    {
                        await _appointmentService.DoPaymentForAppointment(info, cancellationToken);
                    }
                    await _cacheService.RemoveAsync(transaction.Description, cancellationToken);
                }
            }
        }

        _logger.LogInformation("Checked {count} transactions.", transactions.Count);
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

    public async Task<bool> CheckPaymentExisting(Guid id)
    {
        try
        {
            return await _context.Payments.AnyAsync(t => t.Id == id);
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<PaymentResponse>> GetALlPayment(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            var currentUser = _currentUserService.GetRole();
            if (currentUser.Equals(FSHRoles.Patient))
            {
                if (filter.AdvancedSearch == null)
                {
                    filter.AdvancedSearch = new Search();
                    filter.AdvancedSearch.Fields = new List<string>();
                }
                filter.AdvancedSearch.Fields.Add("PatientProfileId");
                var patientProfile = await _context.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                filter.AdvancedSearch.Keyword = patientProfile.Id.ToString();
            }

            var result = new List<PaymentResponse>();

            var paymentQuery = _context.Payments
                    .AsNoTracking();

            if(date != default)
            {
                paymentQuery = paymentQuery.Where(p => p.FinalPaymentDate == date);
            }
            var count = paymentQuery.Count();
            var spec = new EntitiesByPaginationFilterSpec<Payment>(filter);
            paymentQuery = paymentQuery.WithSpecification(spec);

            var payments = await paymentQuery
                .Select(p => new
                {
                    Payment = p,
                    Patient = _context.PatientProfiles.FirstOrDefault(patient => patient.Id == p.PatientProfileId),
                    Service = _context.Services.FirstOrDefault(service => service.Id == p.ServiceId),
                    Appointment = _context.Appointments.FirstOrDefault(appointment => appointment.Id == p.AppointmentId),
                }).ToListAsync(cancellationToken);

            foreach ( var payment in payments)
            {
                var patient = await _userManager.FindByIdAsync(payment.Patient.UserId);

                var response = new PaymentResponse
                {
                    AppointmentId = payment.Appointment.Id,
                    ServiceId = payment.Service.Id,
                    ServiceName = payment.Service.ServiceName,
                    PaymentId = payment.Payment.Id,
                    PatientProfileId = payment.Patient.Id,
                    PatientCode = payment.Patient.PatientCode,
                    PatientName = patient.UserName,
                    DepositAmount = payment.Payment.DepositAmount!.Value,
                    DepositDate = payment.Payment.DepositAmount.Value == 0 ? payment.Payment.DepositDate : default,
                    RemainingAmount = payment.Payment.RemainingAmount!.Value,
                    TotalAmount = payment.Payment.Amount!.Value,
                    Method = Domain.Payments.PaymentMethod.None,
                    Status = payment.Payment.Status,
                };
                result.Add(response);
            }

            return new PaginationResponse<PaymentResponse>(result, count, filter.PageNumber, filter.PageSize);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaymentDetailResponse> GetPaymentDetail(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var query = await _context.Payments
                .Where(p => p.Id == id)
                .Select(a => new
                {
                    Payment = a,
                    pProfile = _context.PatientProfiles.FirstOrDefault(p => p.Id == a.PatientProfileId),
                    Service = _context.Services.FirstOrDefault(p => p.Id == a.ServiceId),
                    Detail = _context.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList()
                })
                .FirstOrDefaultAsync();

            if (query == null)
            {
                throw new Exception("The Payment can not found.");
            }

            var patient = await _userManager.FindByIdAsync(query.pProfile.UserId);

            var response = new PaymentDetailResponse
            {
                PaymentResponse = new PaymentResponse
                {
                    AppointmentId = id,
                    ServiceId = query.Service.Id,
                    ServiceName = query.Service.ServiceName,
                    PaymentId = query.Payment.Id,
                    PatientProfileId = query.pProfile.Id,
                    PatientCode = query.pProfile.PatientCode,
                    PatientName = patient.UserName,
                    DepositAmount = query.Payment.DepositAmount!.Value,
                    DepositDate = query.Payment.DepositAmount.Value == 0 ? query.Payment.DepositDate : default,
                    RemainingAmount = query.Payment.RemainingAmount!.Value,
                    TotalAmount = query.Payment.Amount!.Value,
                    Method = Domain.Payments.PaymentMethod.None,
                    Status = query.Payment.Status,
                },
                Details = new List<Application.Payments.PaymentDetail>()
            };

            foreach (var item in query.Detail)
            {
                var pro = await _context.Procedures.FirstOrDefaultAsync(p => p.Id == item.ProcedureID);
                response.Details.Add(new Application.Payments.PaymentDetail
                {
                    ProcedureID = item.ProcedureID,
                    ProcedureName = pro.Name,
                    PaymentAmount = item.PaymentAmount,
                    PaymentStatus = item.PaymentStatus
                });
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<Transaction>> GetAllTransactions(PaginationFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<Transaction>(filter);
            var result = await _context.Transactions
                .AsNoTracking()
                .WithSpecification(spec)
                .OrderByDescending(x => x.TransactionDate)
                .ToListAsync(cancellationToken);

            var count = await _context.Transactions.CountAsync();

            return new PaginationResponse<Transaction>(result, count, filter.PageNumber, filter.PageSize);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }
}