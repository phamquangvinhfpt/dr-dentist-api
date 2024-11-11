using Amazon.Runtime.Internal.Util;
using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.DentalServices;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Appointments;
internal class AppointmentService : IAppointmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJobService _jobService;
    private readonly ILogger<AppointmentService> _logger;
    private readonly IWorkingCalendarService _workingCalendarService;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;
    public AppointmentService(
        ApplicationDbContext db,
        ICacheService cacheService,
        IStringLocalizer<AppointmentService> t,
        ICurrentUser currentUserService,
        UserManager<ApplicationUser> userManager,
        IJobService jobService,
        ILogger<AppointmentService> logger,
        IWorkingCalendarService workingCalendarService,
        INotificationService notificationService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
        _workingCalendarService = workingCalendarService;
        _cacheService = cacheService;
        _notificationService = notificationService;
    }

    public Task<bool> CheckAppointmentDateValid(DateOnly date)
    {
        return Task.FromResult(date >= DateOnly.FromDateTime(DateTime.Now));
    }

    public async Task<bool> CheckAppointmentExisting(Guid appointmentId)
    {
        var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentId);
        return appointment is not null;
    }

    public async Task<bool> CheckAvailableAppointment(string? patientId)
    {
        var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == patientId);
        var appointment = await _db.Appointments
            .Where(p => p.PatientId == patient.Id &&
            (p.Status == Domain.Appointments.AppointmentStatus.Pending || p.Status == AppointmentStatus.Confirmed)
            ).AnyAsync();
        return !appointment;
    }

    public async Task<AppointmentDepositRequest> CreateAppointment(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        bool hasDoctor = request.DentistId == null;
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DentistId);
        var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == request.PatientId);
        var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceId);

        var currentUserRole = _currentUserService.GetRole();
        var isStaffOrAdmin = currentUserRole == FSHRoles.Admin || currentUserRole == FSHRoles.Staff;

        var app = new Domain.Appointments.Appointment
        {
            PatientId = patient.Id,
            ServiceId = service.Id,
            AppointmentDate = request.AppointmentDate,
            StartTime = request.StartTime,
            Duration = request.Duration,
            Status = isStaffOrAdmin ? AppointmentStatus.Confirmed : AppointmentStatus.Pending,
            Notes = request.Notes,
        };
        if (!hasDoctor)
        {
            app.DentistId = doctor.Id;
        }
        var appointment = _db.Appointments.Add(app).Entity;
        var cal = new Domain.Identity.WorkingCalendar
        {
            AppointmentId = appointment.Id,
            Date = appointment.AppointmentDate,
            StartTime = appointment.StartTime,
            EndTime = appointment.StartTime.Add(appointment.Duration),
            Status = isStaffOrAdmin
            ? Domain.Identity.CalendarStatus.Booked
            : Domain.Identity.CalendarStatus.Waiting,
            Type = AppointmentType.Appointment,
        };
        if (!hasDoctor)
        {
            cal.DoctorId = doctor.Id;
        }
        var calendar = _db.WorkingCalendars.Add(cal).Entity;
        var pay = _db.Payments.Add(new Domain.Payments.Payment
        {
            CreatedBy = _currentUserService.GetUserId(),
            CreatedOn = DateTime.Now,
            PatientProfileId = appointment.PatientId,
            ServiceId = service.Id,
            AppointmentId = appointment.Id,
            DepositAmount = isStaffOrAdmin ? 0 : service.TotalPrice * 0.3,
            DepositDate = isStaffOrAdmin ? null : DateOnly.FromDateTime(DateTime.Now),
            RemainingAmount = isStaffOrAdmin ? service.TotalPrice : service.TotalPrice - (service.TotalPrice * 0.3),
            Amount = service.TotalPrice,
            Status = isStaffOrAdmin ? Domain.Payments.PaymentStatus.Incomplete : Domain.Payments.PaymentStatus.Waiting,
        }).Entity;
        await _db.SaveChangesAsync(cancellationToken);
        if (isStaffOrAdmin)
        {
            _jobService.Schedule(() => CreateTreatmentPlanAndPaymentDetail(appointment.ServiceId, pay.Id, appointment.Id, cancellationToken), TimeSpan.FromSeconds(5));
        }

        var result = new AppointmentDepositRequest
        {
            Key = isStaffOrAdmin ? null : _jobService.Schedule(() => DeleteUnpaidBooking(request.PatientId!, appointment.Id, calendar.Id, pay.Id, cancellationToken), TimeSpan.FromMinutes(11)),
            AppointmentId = appointment.Id,
            PaymentID = pay.Id,
            PatientCode = patient.PatientCode,
            //DepositAmount = isStaffOrAdmin ? 0 : service.TotalPrice * 0.3,
            DepositAmount = 10000,
            DepositTime = isStaffOrAdmin ? TimeSpan.FromMinutes(0) : TimeSpan.FromMinutes(10),
            IsDeposit = isStaffOrAdmin,
        };
        if (!isStaffOrAdmin)
        {
            await _cacheService.SetAsync(patient.PatientCode!, result, TimeSpan.FromMinutes(11), cancellationToken);
        }
        return result;
    }

    public async Task VerifyAndFinishBooking(AppointmentDepositRequest request, CancellationToken cancellationToken)
    {
        var check = _jobService.Delete(request.Key!);
        if (!check)
        {
            throw new KeyNotFoundException("Key job not found");
        }
        var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentId);
        var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentID);

        appointment.Status = AppointmentStatus.Confirmed;
        calendar.Status = Domain.Identity.CalendarStatus.Booked;
        payment.Status = Domain.Payments.PaymentStatus.Incomplete;

        await CreateTreatmentPlanAndPaymentDetail(appointment.ServiceId, payment.Id, appointment.Id, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        _jobService.Schedule(() => SendAppointmentActionNotification(appointment.PatientId,
            appointment.DentistId,
            appointment.AppointmentDate,
            TypeRequest.Verify, cancellationToken), TimeSpan.FromSeconds(5));
    }

    public async Task DeleteUnpaidBooking(string userID, Guid appointmentId, Guid calendarID, Guid paymentID, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentId);
            if (appointment.Status.Equals(AppointmentStatus.Confirmed))
            {
                return;
            }
            var user = await _db.Users.FirstOrDefaultAsync(p => p.Id == _currentUserService.GetUserId().ToString());
            if (user.AccessFailedCount == 3) {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(7));
            }
            else
            {
                user.AccessFailedCount += 1;
            }

            var pay = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentID);
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.Id == calendarID);

            calendar.Status = Domain.Identity.CalendarStatus.Failed;
            _db.WorkingCalendars.Remove(calendar);
            pay.Status = Domain.Payments.PaymentStatus.Failed;
            appointment.Status = AppointmentStatus.Failed;
            _db.Appointments.Remove(appointment);
            _db.Payments.Remove(pay);
            await _db.SaveChangesAsync(cancellationToken);
        }catch(Exception ex)
        {
            _logger.LogError(ex ,ex.Message, null);
        }
    }

    public async Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserService.GetRole();
        if (currentUser.Equals(FSHRoles.Patient))
        {
            if(filter.AdvancedSearch == null)
            {
                filter.AdvancedSearch = new Search();
                filter.AdvancedSearch.Fields = new List<string>();
            }
            filter.AdvancedSearch.Fields.Add("PatientId");
            var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
            filter.AdvancedSearch.Keyword = patientProfile.Id.ToString();
        }
        else if (currentUser.Equals(FSHRoles.Dentist))
        {
            if (filter.AdvancedSearch == null)
            {
                filter.AdvancedSearch = new Search();
                filter.AdvancedSearch.Fields = new List<string>();
            }
            filter.AdvancedSearch.Fields.Add("DentistId");
            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == _currentUserService.GetUserId().ToString());
            filter.AdvancedSearch.Keyword = dProfile.Id.ToString();
        }
        var result = new List<AppointmentResponse>();
        var spec = new EntitiesByPaginationFilterSpec<Appointment>(filter);
        var appointmentsQuery = _db.Appointments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .WithSpecification(spec);

        var count = await appointmentsQuery.CountAsync(cancellationToken);

        var appointments = await appointmentsQuery
            .Select(appointment => new
            {
                Appointment = appointment,
                Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DentistId),
                Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
            })
            .ToListAsync(cancellationToken);

        foreach (var a in appointments)
        {
            result.Add(new AppointmentResponse
            {
                AppointmentId = a.Appointment.Id,
                PatientId = a.Appointment.PatientId,
                DentistId = a.Appointment.DentistId,
                ServiceId = a.Appointment.ServiceId,
                AppointmentDate = a.Appointment.AppointmentDate,
                StartTime = a.Appointment.StartTime,
                Duration = a.Appointment.Duration,
                Status = a.Appointment.Status,
                Notes = a.Appointment.Notes,

                PatientCode = a.Patient?.PatientCode,
                PatientName = _db.Users.FirstOrDefaultAsync(p => p.Id == a.Patient.UserId).Result.UserName,
                DentistName = _db.Users.FirstOrDefaultAsync(p => p.Id == a.Doctor.DoctorId).Result.UserName,
                ServiceName = a.Service?.ServiceName,
                ServicePrice = a.Service?.TotalPrice ?? 0,
                PaymentStatus = a.Payment is not null ? a.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
            });
        }
        return new PaginationResponse<AppointmentResponse>(result, count, filter.PageNumber, filter.PageSize);
    }

    public async Task RescheduleAppointment(RescheduleRequest request, CancellationToken cancellationToken)
    {
        var user_role = _currentUserService.GetRole();
        var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID);
        if (user_role == FSHRoles.Patient)
        {
            if (appointment.SpamCount < 3)
            {
                appointment.SpamCount += 1;
            }
            else
            {
                var user = await _userManager.FindByIdAsync(_currentUserService.GetUserId().ToString());
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(7));
                appointment.Status = AppointmentStatus.Failed;
                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
                calendar.Status = CalendarStatus.Failed;
                var pay = await _db.Payments.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
                pay.Status = Domain.Payments.PaymentStatus.Canceled;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }
        }
        if (user_role == FSHRoles.Patient) {
            var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
            if (appointment.PatientId != patientProfile.Id) {
                throw new UnauthorizedAccessException("Only Patient can reschedule their appointment");
            }
        }
        if (appointment.DentistId != null)
        {
            var check = _workingCalendarService.CheckAvailableTimeSlotToReschedule(appointment.Id,
                request.AppointmentDate,
                request.StartTime,
                request.StartTime.Add(request.Duration)).Result;
            if (!check) {
                throw new Exception("The selected time slot overlaps with an existing appointment");
            }
        }
        appointment.AppointmentDate = request.AppointmentDate;
        appointment.StartTime = request.StartTime;
        appointment.Duration = request.Duration;
        appointment.LastModifiedBy = _currentUserService.GetUserId();
        appointment.LastModifiedOn = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);

        if(appointment.DentistId != null)
        {
            _jobService.Schedule(() => SendAppointmentActionNotification(appointment.PatientId,
            appointment.DentistId,
            appointment.AppointmentDate,
            TypeRequest.Reschedule, cancellationToken), TimeSpan.FromSeconds(5));
        }
    }

    public async Task<bool> CheckAppointmentAvailableToReschedule(Guid appointmentId)
    {
        if (!CheckAppointmentExisting(appointmentId).Result)
        {
            return false;
        }
        else
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentId);
            if (appointment.Status != AppointmentStatus.Confirmed)
            {
                return false;
            }
        }
        return true;
    }

    public async Task CancelAppointment(CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        var user_role = _currentUserService.GetRole();
        if (user_role == FSHRoles.Patient)
        {
            if (request.UserID != _currentUserService.GetUserId().ToString())
            {
                throw new Exception("Only Patient can cancel their appointment");
            }
        }
        var appoint = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID);
        var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);
        appoint.Status = AppointmentStatus.Cancelled;
        calendar.Status = CalendarStatus.Canceled;
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);
        payment.Status = Domain.Payments.PaymentStatus.Canceled;

        await _db.SaveChangesAsync(cancellationToken);

        
        _jobService.Schedule(() => SendAppointmentActionNotification(appoint.PatientId,
            appoint.DentistId,
            appoint.AppointmentDate,
            TypeRequest.Cancel, cancellationToken), TimeSpan.FromSeconds(5));
    }

    public async Task ScheduleAppointment(ScheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        var currentUserRole = _currentUserService.GetRole();
        var isStaffOrAdmin = currentUserRole == FSHRoles.Admin || currentUserRole == FSHRoles.Staff;
        if (!isStaffOrAdmin) {
            throw new UnauthorizedAccessException("Only Staff or Admin can access this function.");
        }
        var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID, cancellationToken);

        if (appointment.Status != AppointmentStatus.Confirmed) {
            throw new Exception("Appointment is not in status to schedule.");
        }

        var check = _workingCalendarService.CheckAvailableTimeSlot(
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.StartTime.Add(appointment.Duration),
            request.DoctorID).Result;

        if (!check)
        {
            throw new InvalidDataException("The selected time slot overlaps with an existing appointment");
        }
        var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DoctorID);
        var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);
        appointment.DentistId = dprofile.Id;
        calendar.DoctorId = dprofile.Id;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SendAppointmentActionNotification(Guid patientID, Guid DoctorID, DateOnly AppointmentDate, TypeRequest type, CancellationToken cancellationToken)
    {
        var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.Id == DoctorID);
        var doctor = await _userManager.FindByIdAsync(dprofile.DoctorId);

        var pprofile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == patientID);
        var patient = await _userManager.FindByIdAsync(pprofile.UserId);

        switch (type)
        {
            case TypeRequest.Verify:
                await _notificationService.SendNotificationToUser(doctor.Id,
                    new Shared.Notifications.BasicNotification
                    {
                        Label = Shared.Notifications.BasicNotification.LabelType.Success,
                        Message = $"You has a meet with patient {patient.UserName} in {AppointmentDate}",
                        Title = "Booking Schedule Notification",
                        Url = null,
                    }, DateTime.Now, cancellationToken);
                break;
            case TypeRequest.Reschedule:
                await _notificationService.SendNotificationToUser(doctor.Id,
                    new Shared.Notifications.BasicNotification
                    {
                        Label = Shared.Notifications.BasicNotification.LabelType.Success,
                        Message = $"Patient {patient.UserName} was reschedule to {AppointmentDate}",
                        Title = "Reschedule Appointment Notification",
                        Url = null,
                    }, DateTime.Now, cancellationToken);
                break;
            case TypeRequest.Cancel:
                await _notificationService.SendNotificationToUser(doctor.Id,
                    new Shared.Notifications.BasicNotification
                    {
                        Label = Shared.Notifications.BasicNotification.LabelType.Success,
                        Message = $"Patient {patient.UserName} was cancel the meeting in {AppointmentDate}",
                        Title = "Cancel Appointment Notification",
                        Url = null,
                    }, DateTime.Now, cancellationToken);
                break;
        }

    }

    public async Task CreateTreatmentPlanAndPaymentDetail(Guid serviceID, Guid paymentID, Guid appointmentID, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentID);
            var groupService = await _db.ServiceProcedures
            .Where(p => p.ServiceId == serviceID)
            .GroupBy(p => p.ServiceId)
            .Select(group => new
            {
                Procedures = group.Select(p => p.ProcedureId).Distinct().ToList(),
            }).FirstOrDefaultAsync(cancellationToken);

            foreach (var item in groupService.Procedures)
            {
                var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item);
                _db.PaymentDetails.Add(new Domain.Payments.PaymentDetail
                {
                    ProcedureID = item!.Value,
                    PaymentID = paymentID,
                    PaymentDay = DateOnly.FromDateTime(DateTime.Now),
                    PaymentAmount = pro.Price - (pro.Price * 0.3),
                    PaymentStatus = Domain.Payments.PaymentStatus.Incomplete,
                });
                var sp = await _db.ServiceProcedures.FirstOrDefaultAsync(p => p.ServiceId == serviceID && p.ProcedureId == item);
                _db.TreatmentPlanProcedures.Add(new Domain.Treatment.TreatmentPlanProcedures
                {
                    ServiceProcedureId = sp.Id,
                    AppointmentID = appointmentID,
                    DoctorID = appointment.DentistId,
                    Status = Domain.Treatment.TreatmentPlanStatus.Active,
                    Price = pro.Price,
                    DiscountAmount = 0.3,
                    TotalCost = pro.Price - (pro.Price * 0.3),
                });
            }
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
        }
    }
}
