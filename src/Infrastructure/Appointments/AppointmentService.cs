using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Bibliography;
using FluentValidation;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Application.TreatmentPlan;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Configuration;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
using System.Numerics;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    private static string KEY_STAFF = "STAFF";
    private static string KEY_DENTIST = "DENTIST";
    private static string KEY_ADMIN = "ADMIN";
    private static string KEY_PATIENT = "PATIENT";
    private static string APPOINTMENT = "APPOINTMENT";
    private static string[] ACTION = { "Get", "GetbyID" };
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

    public async Task<PayAppointmentRequest> CreateAppointment(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            bool hasDoctor = request.DentistId == null;

            var doctor = hasDoctor ? null : await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DentistId)
                ?? throw new NotFoundException($"Doctor with ID {request.DentistId} not found");

            var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == request.PatientId)
                ?? throw new NotFoundException($"Patient with ID {request.PatientId} not found");

            var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceId)
                ?? throw new NotFoundException($"Service with ID {request.ServiceId} not found");

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
                canFeedback = false,
            };

            if (!hasDoctor)
            {
                app.DentistId = doctor.Id;
            }

            var appointment = _db.Appointments.Add(app).Entity;

            var cal = new Domain.Identity.WorkingCalendar
            {
                PatientId = patient.Id,
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

            var result = new PayAppointmentRequest
            {
                Key = isStaffOrAdmin ? null : _jobService.Schedule(
                    () => DeleteUnpaidBooking(request.PatientId!, appointment.Id, calendar.Id, pay.Id, cancellationToken),
                    TimeSpan.FromMinutes(11)),
                AppointmentId = appointment.Id,
                PaymentID = pay.Id,
                PatientCode = patient.PatientCode,
                Amount = 10000,
                Time = isStaffOrAdmin ? TimeSpan.FromMinutes(0) : TimeSpan.FromMinutes(10),
                IsPay = isStaffOrAdmin,
                IsVerify = true,
            };

            if (!isStaffOrAdmin)
            {
                await _cacheService.SetAsync(
                        patient.PatientCode!,
                        result,
                        TimeSpan.FromMinutes(11),
                        cancellationToken);
            }
            _jobService.Schedule(
                    () => DeleteKeyRedisAppointment(),
                    TimeSpan.FromSeconds(2));
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new ApplicationException("An error occurred while creating the appointment", ex);
        }
    }

    private async void DeleteKeyRedisAppointment()
    {
        try
        {
            List<string> KEY = await _cacheService.GetAsync<List<string>>(APPOINTMENT);
            if(KEY.Count() > 0)
            {
                await _cacheService.RemoveAsync(APPOINTMENT);
            }
        } catch (Exception ex) {
            _logger.LogError(ex.Message);
        }
    }

    public async Task VerifyAndFinishBooking(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var check = _jobService.Delete(request.Key!);
            if (!check)
            {
                throw new KeyNotFoundException("Key job not found");
            }
            await _cacheService.RemoveAsync(request.PatientCode!);
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentId);
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);
            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == request.PaymentID);
            var patientId = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == appointment.PatientId);

            appointment.Status = AppointmentStatus.Confirmed;
            calendar.Status = Domain.Identity.CalendarStatus.Booked;
            payment.Status = Domain.Payments.PaymentStatus.Incomplete;

            await _db.SaveChangesAsync(cancellationToken);
            _jobService.Schedule(
                () => SendAppointmentActionNotification(appointment.PatientId,
                appointment.DentistId,
                appointment.AppointmentDate,
                TypeRequest.Verify, cancellationToken), TimeSpan.FromSeconds(5));
            var notification = new BasicNotification
            {
                Message = "Your appointment has been confirmed!",
                Label = BasicNotification.LabelType.Success,
                Title = "Booking Successfully!",
                Url = "/appointment",
            };
            await _notificationService.SendPaymentNotificationToUser(patientId.UserId, notification, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
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
            if (user.AccessFailedCount == 3)
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTime.UtcNow.AddDays(7));
                // send mail to notify block action
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message, null);
        }
    }

    public async Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            string key = APPOINTMENT + _currentUserService.GetUserId();
            var currentUser = _currentUserService.GetRole();
            int count = 0;
            var o = await _cacheService.GetAsync<List<AppointmentResponse>>(key);

            if (o != null) {
                var query = _db.Appointments
                        .IgnoreQueryFilters()
                        .AsNoTracking();
                if (currentUser == FSHRoles.Dentist || currentUser == FSHRoles.Patient)
                {
                    count = query.Count();
                }
                if (currentUser == FSHRoles.Staff || currentUser == FSHRoles.Admin)
                {
                    count = query.Count();
                }
                return new PaginationResponse<AppointmentResponse>(o, count, filter.PageNumber, filter.PageSize)
            }
            if (currentUser.Equals(FSHRoles.Patient))
            {
                if (filter.AdvancedSearch == null)
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
                .AsNoTracking();

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.AppointmentDate == date);
            }
            
            appointmentsQuery = appointmentsQuery.Where(p => p.DentistId != Guid.Empty);

            if (currentUser == FSHRoles.Staff || currentUser == FSHRoles.Admin)
            {
                count = appointmentsQuery.Count();
            }

            appointmentsQuery = appointmentsQuery.WithSpecification(spec).OrderBy(p => p.AppointmentDate);

            if (currentUser == FSHRoles.Dentist || currentUser == FSHRoles.Patient)
            {
                count = appointmentsQuery.Count();
            }

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DentistId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                    Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                bool feedback = await _db.Feedbacks.AnyAsync(p => p.AppointmentId == a.Appointment.Id);
                var dUser = await _userManager.FindByIdAsync(a.Doctor.DoctorId);
                var pUser = _db.Users.FirstOrDefaultAsync(p => p.Id == a.Patient.UserId).Result;
                var r = new AppointmentResponse
                {
                    PatientUserID = pUser.Id,
                    AppointmentId = a.Appointment.Id,
                    PatientId = a.Appointment.PatientId,
                    ServiceId = a.Appointment.ServiceId,
                    AppointmentDate = a.Appointment.AppointmentDate,
                    StartTime = a.Appointment.StartTime,
                    Duration = a.Appointment.Duration,
                    Status = a.Appointment.Status,
                    Notes = a.Appointment.Notes,

                    canFeedback = a.Appointment.canFeedback,
                    isFeedback = feedback,

                    DentistId = a.Appointment.DentistId,
                    DentistName = $"{dUser.FirstName} {dUser.LastName}",
                    PatientCode = a.Patient?.PatientCode,
                    PatientName = $"{pUser.FirstName} {pUser.LastName}",
                    ServiceName = a.Service?.ServiceName,
                    ServicePrice = a.Service?.TotalPrice ?? 0,
                    PaymentStatus = a.Payment is not null ? a.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
                };
                result.Add(r);
            }
            key = APPOINTMENT + _currentUserService.GetUserId();
            List<string> KEY = _cacheService.Get<List<string>>(APPOINTMENT);
            if (KEY != null)
            {
                KEY.Add(key);
                await _cacheService.RefreshAsync(APPOINTMENT);
            }
            else
            {
                List<string> r = new List<string>();
                r.Add(key);
                await _cacheService.SetAsync(APPOINTMENT, KEY);
            }
            await _cacheService.SetAsync(key, result);
            return new PaginationResponse<AppointmentResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task RescheduleAppointment(RescheduleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user_role = _currentUserService.GetRole();
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID) ?? throw new NotFoundException("Error when find appointment.");

            if (user_role == FSHRoles.Patient)
            {
                var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                if (appointment.PatientId != patientProfile.Id)
                {
                    throw new UnauthorizedAccessException("Only Patient can reschedule their appointment");
                }
            }
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
            if (appointment.DentistId != null)
            {
                var check = _workingCalendarService.CheckAvailableTimeSlotToReschedule(appointment.Id,
                    request.AppointmentDate,
                    request.StartTime,
                    request.StartTime.Add(request.Duration)).Result;
                if (!check)
                {
                    throw new Exception("The selected time slot overlaps with an existing appointment");
                }
            }
            appointment.AppointmentDate = request.AppointmentDate;
            appointment.StartTime = request.StartTime;
            appointment.Duration = request.Duration;
            appointment.LastModifiedBy = _currentUserService.GetUserId();
            appointment.LastModifiedOn = DateTime.Now;
            await _db.SaveChangesAsync(cancellationToken);

            if (appointment.DentistId != null)
            {
                _jobService.Schedule(() => SendAppointmentActionNotification(appointment.PatientId,
                appointment.DentistId,
                appointment.AppointmentDate,
                TypeRequest.Reschedule, cancellationToken), TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
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
        try
        {
            var user_role = _currentUserService.GetRole();
            var appoint = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID && p.PatientId == request.UserID);
            if (appoint == null) {
                throw new Exception("Can not found appointment of this user.");
            }
            if (user_role == FSHRoles.Patient)
            {
                var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == request.UserID);
                if (patient.UserId != _currentUserService.GetUserId().ToString())
                {
                    throw new Exception("Only Patient can cancel their appointment");
                }
            }
            
            if (appoint.Status == AppointmentStatus.Confirmed) {
                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);

                calendar.Status = CalendarStatus.Canceled;
                var payment = await _db.Payments.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);
                payment.Status = Domain.Payments.PaymentStatus.Canceled;
                if(appoint.DentistId != Guid.Empty)
                {
                    _jobService.Schedule(() => SendAppointmentActionNotification(appoint.PatientId,
                        appoint.DentistId,
                        appoint.AppointmentDate,
                        TypeRequest.Cancel, cancellationToken), TimeSpan.FromSeconds(5));
                }
            }
            else if(appoint.Status == AppointmentStatus.Success)
            {
                var query = await _db.TreatmentPlanProcedures
                    .Where(p => p.AppointmentID == request.AppointmentID && p.Status == Domain.Treatment.TreatmentPlanStatus.Active).OrderByDescending(p => p.StartDate).ToListAsync();
                foreach (var item in query) {
                    item.Status = Domain.Treatment.TreatmentPlanStatus.Cancelled;
                    var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.PlanID == item.Id);
                    calendar.Status = CalendarStatus.Canceled;
                    _jobService.Schedule(() => SendAppointmentActionNotification(appoint.PatientId,
                        appoint.DentistId,
                        calendar.Date.Value,
                        TypeRequest.Cancel, cancellationToken), TimeSpan.FromSeconds(5));
                }
            }
            else
            {
                throw new BadRequestException("The Appointment can not be cancel");
            }
            appoint.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task ScheduleAppointment(ScheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var currentUserRole = _currentUserService.GetRole();
            var isStaffOrAdmin = currentUserRole == FSHRoles.Admin || currentUserRole == FSHRoles.Staff;
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID, cancellationToken);
            if (currentUserRole == FSHRoles.Patient)
            {
                var pProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUserService.GetUserId().ToString());
                if (pProfile.Id != appointment.PatientId)
                {
                    throw new Exception("Only Patient can reschedule their appointment");
                }
            }
            else if(!isStaffOrAdmin)
            {
                throw new UnauthorizedAccessException("Only Staff or Admin can access this function.");
            }

            if (appointment.Status != AppointmentStatus.Confirmed)
            {
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
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task SendAppointmentActionNotification(Guid patientID, Guid DoctorID, DateOnly AppointmentDate, TypeRequest type, CancellationToken cancellationToken)
    {
        try
        {
            var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.Id == DoctorID);
            var doctor = await _userManager.FindByIdAsync(dprofile.DoctorId);
            var pprofile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == patientID);
            var patient = await _userManager.FindByIdAsync(pprofile.UserId);

            switch (type)
            {
                case TypeRequest.Verify:
                    await _notificationService.SendNotificationToUser(dprofile.DoctorId,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"You has a meet with patient {patient.FirstName} {patient.LastName} in {AppointmentDate}",
                            Title = "Booking Schedule Notification",
                            Url = null,
                        }, null, cancellationToken);
                    break;
                case TypeRequest.Reschedule:
                    await _notificationService.SendNotificationToUser(doctor.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"Patient {patient.FirstName} {patient.LastName} was reschedule to {AppointmentDate}",
                            Title = "Reschedule Appointment Notification",
                            Url = null,
                        }, null, cancellationToken);
                    break;
                case TypeRequest.Cancel:
                    await _notificationService.SendNotificationToUser(doctor.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Success,
                            Message = $"Patient {patient.FirstName} {patient.LastName} was cancel the meeting in {AppointmentDate}",
                            Title = "Cancel Appointment Notification",
                            Url = null,
                        }, null, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    public async Task<AppointmentResponse> GetAppointmentByID(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var appointments = await _db.Appointments
                .Where(p => p.Id == id)
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                    Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
                })
                .FirstOrDefaultAsync(cancellationToken);

            bool isFeedback = await _db.Feedbacks.AnyAsync(p => p.AppointmentId == id);
            var patient = await _userManager.FindByIdAsync(appointments.Patient.UserId);
            var result = new AppointmentResponse
            {
                PatientUserID = patient.Id,
                AppointmentId = appointments.Appointment.Id,
                PatientId = appointments.Appointment.PatientId,
                ServiceId = appointments.Appointment.ServiceId,
                AppointmentDate = appointments.Appointment.AppointmentDate,
                StartTime = appointments.Appointment.StartTime,
                Duration = appointments.Appointment.Duration,
                Status = appointments.Appointment.Status,
                Notes = appointments.Appointment.Notes,
                canFeedback = appointments.Appointment.canFeedback,
                isFeedback = isFeedback,
                PatientCode = appointments.Patient?.PatientCode,
                PatientName = $"{patient.FirstName} {patient.LastName}",
                ServiceName = appointments.Service?.ServiceName,
                ServicePrice = appointments.Service?.TotalPrice ?? 0,
                PaymentStatus = appointments.Payment is not null ? appointments.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
            };

            if(appointments.Appointment.DentistId != Guid.Empty)
            {
                var dentist = _db.DoctorProfiles.FirstOrDefault(p => p.Id == appointments.Appointment.DentistId);
                if (dentist != null) {
                    var d = await _userManager.FindByIdAsync(dentist.DoctorId);
                    result.DentistId = dentist.Id;
                    result.DentistName = $"{d.FirstName} {d.LastName}";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<TreatmentPlanResponse>> ToggleAppointment(Guid id, CancellationToken cancellationToken)
    {
        var result = new List<TreatmentPlanResponse>();
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == id) ?? throw new KeyNotFoundException("Appointment Not Found.");
            if (appointment.AppointmentDate != DateOnly.FromDateTime(DateTime.Now))
            {
                throw new Exception("The date is not the date.");
            }
            if (appointment.Status == AppointmentStatus.Confirmed)
            {
                var groupService = await _db.ServiceProcedures
                .Where(p => p.ServiceId == appointment.ServiceId)
                .GroupBy(p => p.ServiceId)
                .Select(group => new
                {
                    Procedures = group.Select(p => p.ProcedureId).Distinct().ToList(),
                }).FirstOrDefaultAsync(cancellationToken);

                var payment = _db.Payments.FirstOrDefault(p => p.AppointmentId == id);

                var dprofile = _db.DoctorProfiles.FirstOrDefault(p => p.Id == appointment.DentistId);

                var doctor = _userManager.FindByIdAsync(dprofile.DoctorId!).Result;

                appointment.Status = AppointmentStatus.Success;

                foreach (var item in groupService.Procedures)
                {
                    var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item);
                    _db.PaymentDetails.Add(new Domain.Payments.PaymentDetail
                    {
                        ProcedureID = item!.Value,
                        PaymentID = payment.Id,
                        PaymentDay = DateOnly.FromDateTime(DateTime.Now),
                        PaymentAmount = pro.Price - (pro.Price * 0.3),
                        PaymentStatus = Domain.Payments.PaymentStatus.Incomplete,
                    });
                    var sp = await _db.ServiceProcedures.FirstOrDefaultAsync(p => p.ServiceId == appointment.ServiceId && p.ProcedureId == item);

                    var t = new Domain.Treatment.TreatmentPlanProcedures
                    {
                        ServiceProcedureId = sp.Id,
                        AppointmentID = id,
                        DoctorID = appointment.DentistId,
                        Status = Domain.Treatment.TreatmentPlanStatus.Pending,
                        Price = pro.Price,
                        DiscountAmount = 0.3,
                        TotalCost = pro.Price - (pro.Price * 0.3),
                    };

                    if (sp.StepOrder == 1)
                    {
                        t.StartDate = appointment.AppointmentDate;
                        t.StartTime = appointment.StartTime;
                        t.Status = Domain.Treatment.TreatmentPlanStatus.Active;
                    }

                    var entry = _db.TreatmentPlanProcedures.Add(t).Entity;
                    if(sp.StepOrder == 1)
                    {
                        var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == id);
                        calendar.PlanID = entry.Id;
                        entry.Status = Domain.Treatment.TreatmentPlanStatus.Active;
                        entry.StartDate = appointment.AppointmentDate;
                        entry.StartTime = appointment.StartTime;
                    }
                    var r = new TreatmentPlanResponse
                    {
                        TreatmentPlanID = entry.Id,
                        ProcedureID = item.Value,
                        ProcedureName = pro.Name,
                        Price = pro.Price,
                        DoctorID = doctor.Id,
                        DoctorName = $"{doctor.FirstName} {doctor.LastName}",
                        DiscountAmount = 0.3,
                        PlanCost = entry.TotalCost,
                        PlanDescription = null,
                        Step = sp.StepOrder,
                        Status = entry.Status,
                    };
                    if(sp.StepOrder == 1)
                    {
                        r.StartDate = appointment.AppointmentDate;
                    }
                    result.Add(r);
                }
                await _db.SaveChangesAsync(cancellationToken);
                result = result.OrderBy(p => p.Step).ToList();
            }
            else
            {
                throw new Exception("Can not verify appointment.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
        return result;
    }

    public async Task<PaymentDetailResponse> GetRemainingAmountOfAppointment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var query = await _db.Payments
                .Where(p => p.AppointmentId == id)
                .Select(a => new
                {
                    Payment = a,
                    pProfile = _db.PatientProfiles.FirstOrDefault(p => p.Id == a.PatientProfileId),
                    Service = _db.Services.FirstOrDefault(p => p.Id == a.ServiceId),
                    Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList()
                })
                .FirstOrDefaultAsync();

            if(query.Payment.Status != PaymentStatus.Incomplete)
            {
                throw new Exception("The appointment have no any amount to pay.");
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
                    PatientName = $"{patient.FirstName} {patient.LastName}",
                    DepositAmount = query.Payment.DepositAmount!.Value,
                    DepositDate = query.Payment.DepositAmount.Value == 0 ? query.Payment.DepositDate : default,
                    RemainingAmount = query.Payment.RemainingAmount!.Value,
                    TotalAmount = query.Payment.Amount!.Value,
                    Method = Domain.Payments.PaymentMethod.None,
                    Status = query.Payment.Status,
                },
                Details = new List<Application.Payments.PaymentDetail>()
            };

            foreach(var item in query.Detail)
            {
                var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Id == item.ProcedureID);
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
        catch (Exception ex) {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task HandlePaymentRequest(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if(request.IsPay && (request.Method == Domain.Payments.PaymentMethod.Cash))
            {
                var query = await _db.Payments
                    .Where(p => p.Id == request.PaymentID)
                    .Select(a => new
                    {
                        Payment = a,
                        Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList(),
                    })
                    .FirstOrDefaultAsync();

                if(query.Payment.RemainingAmount != request.Amount)
                {
                    throw new Exception("Warning: Amount is not equal");
                }

                query.Payment.Status = Domain.Payments.PaymentStatus.Completed;
                query.Payment.FinalPaymentDate = DateOnly.FromDateTime(DateTime.Now);

                foreach (var item in query.Detail) {
                    item.PaymentStatus = Domain.Payments.PaymentStatus.Completed;
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
            else if(!request.IsPay && (request.Method == Domain.Payments.PaymentMethod.BankTransfer))
            {
                var query = await _db.Payments
                    .Where(p => p.Id == request.PaymentID)
                    .Select(a => new
                    {
                        Payment = a,
                        Patient = _db.PatientProfiles.FirstOrDefault(e => e.Id == a.PatientProfileId),
                        Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList(),
                    })
                    .FirstOrDefaultAsync();

                var result = new PayAppointmentRequest
                {
                    AppointmentId = request.AppointmentId,
                    PaymentID = request.PaymentID,
                    PatientCode = query.Patient.PatientCode,
                    Amount = query.Payment.RemainingAmount.Value,
                    IsVerify = false,
                };
                await _cacheService.SetAsync(query.Patient.PatientCode, result, request.Time);
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task DoPaymentForAppointment(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var query = await _db.Payments
                    .Where(p => p.Id == request.PaymentID)
                    .Select(a => new
                    {
                        Payment = a,
                        Detail = _db.PaymentDetails.Where(t => t.PaymentID == a.Id).ToList(),
                    })
                    .FirstOrDefaultAsync();

            if (query.Payment.RemainingAmount != request.Amount)
            {
                throw new Exception("Warning: Amount is not equal");
            }

            query.Payment.Status = Domain.Payments.PaymentStatus.Completed;
            query.Payment.FinalPaymentDate = DateOnly.FromDateTime(DateTime.Now);
            query.Payment.RemainingAmount = 0;
            foreach (var item in query.Detail)
            {
                item.PaymentStatus = Domain.Payments.PaymentStatus.Completed;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _cacheService.Remove(request.PatientCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> CancelPayment(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _cacheService.RemoveAsync(request.PatientCode);
            return _t["Success"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<AppointmentResponse>> GetNonDoctorAppointments(PaginationFilter filter, DateOnly date, TimeSpan time, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<AppointmentResponse>();
            var spec = new EntitiesByPaginationFilterSpec<Appointment>(filter);
            var appointmentsQuery = _db.Appointments
                .AsNoTracking().Where(p => p.DentistId == default);

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.AppointmentDate == date);
            }
            if (time != default) {
                appointmentsQuery = appointmentsQuery.Where(w => w.StartTime == time);
            }

            var count = await appointmentsQuery.CountAsync(cancellationToken);

            appointmentsQuery = appointmentsQuery.WithSpecification(spec);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                    Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                var patient = _db.Users.FirstOrDefaultAsync(p => p.Id == a.Patient.UserId).Result;
                result.Add(new AppointmentResponse
                {
                    PatientUserID = patient.Id,
                    AppointmentId = a.Appointment.Id,
                    PatientId = a.Appointment.PatientId,
                    ServiceId = a.Appointment.ServiceId,
                    AppointmentDate = a.Appointment.AppointmentDate,
                    StartTime = a.Appointment.StartTime,
                    Duration = a.Appointment.Duration,
                    Status = a.Appointment.Status,
                    Notes = a.Appointment.Notes,

                    PatientCode = a.Patient?.PatientCode,
                    PatientName = $"{patient.FirstName} {patient.LastName}",
                    ServiceName = a.Service?.ServiceName,
                    ServicePrice = a.Service?.TotalPrice ?? 0,
                    PaymentStatus = a.Payment is not null ? a.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
                });
            }
            return new PaginationResponse<AppointmentResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> AddDoctorToAppointments(AddDoctorToAppointment request, CancellationToken cancellationToken)
    {
        try
        {
            if(request.DoctorID == default || request.AppointmentID == default)
            {
                throw new Exception("All Information should be include.");
            }
            var doctor = await _db.DoctorProfiles
                .Where(p => p.Id == request.DoctorID)
                .FirstOrDefaultAsync();
            if(doctor == null)
            {
                throw new Exception("Doctor can not be found.");
            }
            var user = await _userManager.FindByIdAsync(doctor.DoctorId);
            if (!user.IsActive)
            {
                throw new Exception("Doctor has been deactive.");
            }
            var appoitment = await _db.Appointments
                .Where(p => p.Id == request.AppointmentID && p.Status == AppointmentStatus.Confirmed)
                .Select(c => new
                {
                    Appointment = c,
                    Calendar = _db.WorkingCalendars.FirstOrDefault(p => p.AppointmentId == c.Id)
                })
                .FirstOrDefaultAsync();

            if (appoitment == null) {
                throw new Exception("Appointment can not be found or be cancel.");
            }
            if(appoitment.Appointment.AppointmentDate < DateOnly.FromDateTime(DateTime.Now))
            {
                throw new Exception("Appointment invalid.");
            }

            var check = await _workingCalendarService.CheckAvailableTimeSlot(
                appoitment.Appointment.AppointmentDate,
                appoitment.Appointment.StartTime,
                appoitment.Appointment.StartTime.Add(appoitment.Appointment.Duration),
                request.DoctorID);
            if (!check) {
                throw new Exception("Doctor has a meeting in this time");
            }
            appoitment.Appointment.DentistId = request.DoctorID;
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID);

            calendar.DoctorId = request.DoctorID;

            await _db.SaveChangesAsync(cancellationToken);
            return _t["Success"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetFollowUpAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken)
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
                filter.AdvancedSearch.Fields.Add("DoctorId");
                var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == _currentUserService.GetUserId().ToString());
                filter.AdvancedSearch.Keyword = dProfile.Id.ToString();
            }
            var result = new List<GetWorkingDetailResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);
            var appointmentsQuery = _db.WorkingCalendars
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.Type == AppointmentType.FollowUp);

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.Date == date);
            }

            var count = await appointmentsQuery.CountAsync(cancellationToken);

            appointmentsQuery = appointmentsQuery.WithSpecification(spec);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DoctorId),
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    TreatmentPlan = _db.TreatmentPlanProcedures.FirstOrDefault(s => s.Id == appointment.PlanID),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                var doctor = await _userManager.FindByIdAsync(a.Doctor.DoctorId);
                var patient = await _userManager.FindByIdAsync(a.Patient.UserId);
                var sp = await _db.ServiceProcedures.Where(p => p.Id == a.TreatmentPlan.ServiceProcedureId)
                    .Select(s => new
                    {
                        Service = _db.Services.FirstOrDefault(p => p.Id == s.ServiceId),
                        Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                        Step= s.StepOrder
                    }).FirstOrDefaultAsync();
                result.Add(new GetWorkingDetailResponse
                {
                    AppointmentId = a.Appointment.AppointmentId.Value,
                    AppointmentType = a.Appointment.Type,
                    CalendarID = a.Appointment.Id,
                    Date = a.Appointment.Date.Value,
                    DoctorName = $"{doctor.FirstName} {doctor.LastName}",
                    DoctorProfileID = a.Appointment.DoctorId.Value,
                    EndTime = a.Appointment.EndTime.Value,
                    Note = a.Appointment.Note,
                    PatientCode = a.Patient.PatientCode,
                    PatientName = $"{patient.FirstName} {patient.LastName}",
                    PatientProfileID = a.Patient.Id,
                    ProcedureID = sp.Procedure.Id,
                    ProcedureName = sp.Procedure.Name,
                    ServiceID = sp.Service.Id,
                    ServiceName = sp.Service.ServiceName,
                    StartTime = a.Appointment.StartTime.Value,
                    Status = a.Appointment.Status,
                    Step = sp.Step,
                });
            }
            return new PaginationResponse<GetWorkingDetailResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }
}
