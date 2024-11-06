using Amazon.Runtime.Internal.Util;
using Ardalis.Specification.EntityFrameworkCore;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
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
    public AppointmentService(ApplicationDbContext db,
        IStringLocalizer<AppointmentService> t,
        ICurrentUser currentUserService,
        UserManager<ApplicationUser> userManager,
        IJobService jobService, ILogger<AppointmentService> logger,
       IWorkingCalendarService workingCalendarService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
        _workingCalendarService = workingCalendarService;
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
            p.Status == Domain.Appointments.AppointmentStatus.Pending
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
            Type = request.Type,
            Notes = request.Notes,
        };
        if (hasDoctor)
        {
            app.DentistId = doctor.Id;
        }
        var appointment = _db.Appointments.Add(app).Entity;
        Guid calendarID = Guid.Empty;
        if (hasDoctor)
        {
            calendarID = _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
            {
                DoctorId = doctor.Id,
                AppointmentId = appointment.Id,
                Date = appointment.AppointmentDate,
                StartTime = appointment.StartTime,
                EndTime = appointment.StartTime.Add(appointment.Duration),
                Status = isStaffOrAdmin
            ? Domain.Identity.CalendarStatus.OnGoing
            : Domain.Identity.CalendarStatus.Waiting,
            }).Entity.Id;
        }
        await _db.SaveChangesAsync(cancellationToken);

        return new AppointmentDepositRequest
        {
            Key = _jobService.Schedule(() => DeleteUnpaidBooking(appointment.Id, calendarID, cancellationToken), TimeSpan.FromMinutes(10)),
            AppointmentId = appointment.Id,
            DepositAmount = isStaffOrAdmin ? 0 : service.TotalPrice * 0.3,
            DepositTime = isStaffOrAdmin ? TimeSpan.FromMinutes(0) : TimeSpan.FromMinutes(10),
            IsDeposit = isStaffOrAdmin,
        };
    }

    public async Task VerifyAndFinishBooking(AppointmentDepositRequest request, CancellationToken cancellationToken)
    {
        var check = _jobService.Delete(request.Key);
        if (!check)
        {
            throw new KeyNotFoundException("Key job not found");
        }
        var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentId);
        var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == appointment.ServiceId);
        var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointment.Id);

        appointment.Status = AppointmentStatus.Confirmed;
        calendar.Status = Domain.Identity.CalendarStatus.OnGoing;

        _db.Payments.Add(new Domain.Payments.Payment
        {
            CreatedBy = _currentUserService.GetUserId(),
            CreatedOn = DateTime.Now,
            PatientProfileId = appointment.PatientId,
            ServiceId = service.Id,
            AppointmentId = appointment.Id,
            DepositAmount = request.DepositAmount,
            DepositDate = DateOnly.FromDateTime(DateTime.Now),
            RemainingAmount = service.TotalPrice - request.DepositAmount,
            Amount = service.TotalPrice,
            Status = Domain.Payments.PaymentStatus.Incomplete,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUnpaidBooking(Guid appointmentId, Guid calendarID, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == appointmentId);
            if (appointment.Status.Equals(AppointmentStatus.Confirmed)) {
                return;
            }
            if (calendarID != Guid.Empty) {
                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.Id == calendarID);
                calendar.Status = Domain.Identity.CalendarStatus.Failed;
                _db.WorkingCalendars.Remove(calendar);
            }
            appointment.Status = AppointmentStatus.Fail;
            _db.Appointments.Remove(appointment);
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
                Service = _db.Services.FirstOrDefault(s => s.Id == appointment.ServiceId),
                Payment = _db.Payments.FirstOrDefault(p => p.AppointmentId == appointment.Id),
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
                Type = a.Appointment.Type,
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
    }

    public async Task<bool> CheckAppointmentAvailableToReschedule(DefaultIdType appointmentId)
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
}
