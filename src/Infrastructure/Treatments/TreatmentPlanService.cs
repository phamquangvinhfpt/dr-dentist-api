using DocumentFormat.OpenXml.Vml.Office;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Application.TreatmentPlan;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Infrastructure.Appointments;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Treatments;
internal class TreatmentPlanService : ITreatmentPlanService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJobService _jobService;
    private readonly ILogger<TreatmentPlanService> _logger;
    private readonly IWorkingCalendarService _workingCalendarService;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;

    public TreatmentPlanService(ApplicationDbContext db, IStringLocalizer<TreatmentPlanService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, IJobService jobService, ILogger<TreatmentPlanService> logger, IWorkingCalendarService workingCalendarService, ICacheService cacheService, INotificationService notificationService)
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

    public async Task AddFollowUpAppointment(AddTreatmentDetail request, CancellationToken cancellationToken)
    {
        var appointment = await _db.Appointments.FirstOrDefaultAsync(p => p.Id == request.AppointmentID);
        if (appointment.Status != AppointmentStatus.Success)
        {
            throw new Exception("Appointment in status that can not do this action");
        }

        var plan = await _db.TreatmentPlanProcedures
            .Where(p => p.Id == request.TreatmentId)
            .Select(b => new
            {
                Plan = b,
                SP = _db.ServiceProcedures.FirstOrDefault(p => p.Id == b.ServiceProcedureId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (plan.SP.StepOrder == 1)
        {
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID && p.PlanID == request.TreatmentId);
            calendar.Date = request.TreatmentDate;
            calendar.StartTime = request.TreatmentTime;
            calendar.EndTime = request.TreatmentTime.Add(TimeSpan.FromMinutes(30));
        }
        else
        {
            _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
            {
                DoctorId = appointment.DentistId,
                AppointmentId = request.AppointmentID,
                PlanID = plan.Plan.Id,
                Date = request.TreatmentDate,
                StartTime = request.TreatmentTime,
                EndTime = request.TreatmentTime.Add(TimeSpan.FromMinutes(30)),
                Status = Domain.Identity.CalendarStatus.Booked,
                Note = request.Note,
                Type = AppointmentType.FollowUp,
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> CheckDateValid(DateOnly date)
    {
        return Task.FromResult(date >= DateOnly.FromDateTime(DateTime.Now));
    }

    public async Task<bool> CheckDoctorAvailability(Guid treatmentId, DateOnly treatmentDate, TimeSpan treatmentTime)
    {
        var plan = await _db.TreatmentPlanProcedures.FirstOrDefaultAsync(p => p.Id == treatmentId);

        var check = await _workingCalendarService.CheckAvailableTimeSlotToAddFollowUp(plan.DoctorID.Value, treatmentDate, treatmentTime);

        return check;

    }

    public async Task<bool> CheckPlanExisting(Guid id)
    {
        var result = await _db.TreatmentPlanProcedures.AnyAsync(p => p.Id == id);
        return !result;
    }

    public async Task<List<TreatmentPlanResponse>> GetTreamentPlanByAppointment(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = new List<TreatmentPlanResponse>();
        try
        {
            var tps = await _db.TreatmentPlanProcedures
                .Where(p => p.AppointmentID == appointmentId)
                .ToListAsync(cancellationToken);
            var dprofile = _db.DoctorProfiles.FirstOrDefault(p => p.Id == tps[0].DoctorID);

            var doctor = _userManager.FindByIdAsync(dprofile.DoctorId!).Result;
            foreach (var item in tps) {

                var sp = await _db.ServiceProcedures
                .Where(p => p.Id == item.ServiceProcedureId)
                .Select(b => new
                {
                    SP = b,
                    Procedure = _db.Procedures.FirstOrDefault(p => p.Id == b.ProcedureId),
                })
                .FirstOrDefaultAsync(cancellationToken);

                result.Add(new TreatmentPlanResponse
                {
                    TreatmentPlanID = item.Id,
                    ProcedureID = sp.Procedure.Id,
                    ProcedureName = sp.Procedure.Name,
                    Price = sp.Procedure.Price,
                    DoctorID = doctor.Id,
                    DoctorName = doctor.UserName,
                    DiscountAmount = 0.3,
                    PlanCost = item.TotalCost,
                    PlanDescription = item.Note,
                    Step = sp.SP.StepOrder,
                    Status = item.Status,
                });
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message, ex);
        }
        return result;
    }

    public Task UpdateTreamentPlan()
    {
        throw new NotImplementedException();
    }
}
