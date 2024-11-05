using Amazon.Runtime.Internal.Util;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.Appointments;
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
    public AppointmentService(ApplicationDbContext db,
        IStringLocalizer<AppointmentService> t,
        ICurrentUser currentUserService,
        UserManager<ApplicationUser> userManager,
        IJobService jobService, ILogger<AppointmentService> logger)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
    }

    public Task<bool> CheckAppointmentDateValid(DateOnly date)
    {
        return Task.FromResult(date > DateOnly.FromDateTime(DateTime.Now));
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
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DentistId);
        var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == request.PatientId);
        var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == request.ServiceId);

        var currentUserRole = _currentUserService.GetRole();
        var isStaffOrAdmin = currentUserRole == FSHRoles.Admin || currentUserRole == FSHRoles.Staff;

        var appointment = _db.Appointments.Add(new Domain.Appointments.Appointment
        {
            PatientId = patient.Id,
            DentistId = doctor.Id,
            ServiceId = service.Id,
            AppointmentDate = request.AppointmentDate,
            StartTime = request.StartTime,
            Duration = request.Duration,
            Status = isStaffOrAdmin ? AppointmentStatus.Confirmed : AppointmentStatus.Pending,
            Type = request.Type,
            Notes = request.Notes,
        }).Entity;

        var calendar = _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
        {
            DoctorId = doctor.Id,
            AppointmentId = appointment.Id,
            Date = appointment.AppointmentDate,
            StartTime = appointment.StartTime,
            EndTime = appointment.StartTime.Add(appointment.Duration),
            Status = isStaffOrAdmin
            ? Domain.Identity.CalendarStatus.OnGoing
            : Domain.Identity.CalendarStatus.Waiting,
        }).Entity;
        await _db.SaveChangesAsync(cancellationToken);

        return new AppointmentDepositRequest
        {
            Key = _jobService.Schedule(() => DeleteUnpaidBooking(appointment.Id, calendar.Id, cancellationToken), TimeSpan.FromMinutes(10)),
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
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.Id == calendarID);

            _db.WorkingCalendars.Remove(calendar);
            _db.Appointments.Remove(appointment);
            await _db.SaveChangesAsync(cancellationToken);
        }catch(Exception ex)
        {
            _logger.LogError(ex ,ex.Message, null);
        }
    }
}
