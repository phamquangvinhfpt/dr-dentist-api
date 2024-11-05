using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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

    public AppointmentService(ApplicationDbContext db, IStringLocalizer<AppointmentService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
    }

    public Task<bool> CheckAppointmentDateValid(DateTime date)
    {
        return Task.FromResult(date > DateTime.Now);
    }

    public async Task<bool> CheckAvailableAppointment(string? patientId, Guid serviceId, DateTime appointmentDate)
    {
        var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == patientId);
        var appointment = await _db.Appointments
            .Where(p => p.PatientId == patient.Id &&
            p.AppointmentDate == appointmentDate &&
            p.ServiceId == serviceId &&
            p.Status == Domain.Appointments.AppointmentStatus.Pending
            ).AnyAsync();
        return !appointment;
    }

    public Task<bool> CheckAvailableAppointment(string? patientId, DefaultIdType serviceId, DateTime appointmentDate, TimeSpan startTime, TimeSpan duration)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CheckAvailableTimeSlot(string? dentistId, DateTime appointmentDate, TimeSpan startTime, TimeSpan duration)
    {
        throw new NotImplementedException();
    }
}
