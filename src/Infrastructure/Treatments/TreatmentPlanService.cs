using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Vml.Office;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Application.TreatmentPlan;
using FSH.WebApi.Application.TreatmentPlan.Prescriptions;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Infrastructure.Appointments;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using SQLitePCL;
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
        try
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
                //if (plan.Plan.Status != Domain.Treatment.TreatmentPlanStatus.Active) {

                //}
                //var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID && p.PlanID == request.TreatmentId);
                //calendar.Date = request.TreatmentDate;
                //calendar.StartTime = request.TreatmentTime;
                //calendar.EndTime = request.TreatmentTime.Add(TimeSpan.FromMinutes(30));
                throw new Exception("Warning: the plan was set time line");
            }
            else
            {
                var past_procedure = await _db.ServiceProcedures
                    .Where(p => p.ServiceId == plan.SP.ServiceId && p.StepOrder == (plan.SP.StepOrder - 1))
                    .FirstOrDefaultAsync();

                var past_plan = await _db.TreatmentPlanProcedures
                    .Where(p => p.ServiceProcedureId == past_procedure.Id && p.AppointmentID == appointment.Id)
                    .FirstOrDefaultAsync();

                var hasCalendar = await _db.WorkingCalendars
                    .FirstOrDefaultAsync(p => p.PlanID == past_plan.Id);

                if (hasCalendar == null)
                {
                    throw new Exception("The previous procedure is not done");
                }
                else
                {
                    if (request.TreatmentDate < hasCalendar.Date)
                    {
                        throw new Exception("Warning: the plan can not do when the previous plan in progress");
                    }
                }
                plan.Plan.StartDate = request.TreatmentDate;
                plan.Plan.StartTime = request.TreatmentTime;
                plan.Plan.Status = Domain.Treatment.TreatmentPlanStatus.Active;
                _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
                {
                    PatientId = appointment.PatientId,
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
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task AddPrescription(AddPrescriptionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _db.Prescriptions.AnyAsync(p => p.TreatmentID == request.TreatmentID);
            if (existing) {
                throw new Exception("The Plan has prescription");
            }
            var plan = await _db.TreatmentPlanProcedures
                .FirstOrDefaultAsync(p => p.Id == request.TreatmentID);

            if(plan.StartDate != DateOnly.FromDateTime(DateTime.Now))
            {
                throw new Exception("The plan date is not available");
            }

            if(plan.Status != Domain.Treatment.TreatmentPlanStatus.Completed)
            {
                throw new Exception("The plan have not done yet!!!");
            }

            var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == plan.AppointmentID);
            var entry = _db.Prescriptions
                .Add(new Domain.Treatment.Prescription
                {
                    DoctorID = appointment.DentistId,
                    PatientID = appointment.PatientId,
                    TreatmentID = request.TreatmentID,
                    Notes = request.Notes,
                    CreatedBy = _currentUserService.GetUserId(),
                    CreatedOn = DateTime.Now,
                }).Entity;

            foreach (var item in request.ItemRequests)
            {
                _db.PrescriptionItems.Add(new Domain.Treatment.PrescriptionItem
                {
                    PrescriptionId = entry.Id,
                    MedicineName = item.MedicineName,
                    Dosage = item.Dosage,
                    Frequency = item.Frequency
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
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
        return result;
    }

    public async Task<string> ExaminationAndChangeTreatmentStatus(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _db.TreatmentPlanProcedures
                .FirstOrDefaultAsync(p => p.Id == id);


            if (plan.Status != Domain.Treatment.TreatmentPlanStatus.Active)
            {
                throw new Exception("The plan is not schedule or success");
            }

            if (plan.StartDate != DateOnly.FromDateTime(DateTime.Now))
            {
                throw new Exception("The plan date is not today");
            }

            var cal = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.PlanID == plan.Id)
                ?? throw new Exception("Calendar is not found.");

            plan.Status = Domain.Treatment.TreatmentPlanStatus.Completed;
            cal.Status = Domain.Identity.CalendarStatus.Completed;

            var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == plan.AppointmentID);
            var isCompleted = _db.TreatmentPlanProcedures.Count(p => p.AppointmentID == plan.AppointmentID && p.Status != Domain.Treatment.TreatmentPlanStatus.Completed);

            if (isCompleted == 0)
            {
                appointment.canFeedback = true;
                appointment.Status = AppointmentStatus.Done;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return _t["Success"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<List<TreatmentPlanResponse>> GetCurrentTreamentPlanByPatientID(string id, CancellationToken cancellationToken)
    {
        try
        {
            List<TreatmentPlanResponse> result = new List<TreatmentPlanResponse>();
            var patient = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == id) ?? throw new Exception("Patient not found");
            var newAppointment = await _db.Appointments.Where(p => p.PatientId == patient.Id && p.Status == AppointmentStatus.Success)
                .OrderByDescending(p => p.AppointmentDate)
                .FirstOrDefaultAsync();

            var check = await _db.TreatmentPlanProcedures
                .AnyAsync(p => p.AppointmentID == newAppointment.Id && (p.Status == Domain.Treatment.TreatmentPlanStatus.Active || p.Status == Domain.Treatment.TreatmentPlanStatus.Pending));

            if (!check)
            {
                return result;
            }

            var tps = await _db.TreatmentPlanProcedures
                .Where(p => p.AppointmentID == newAppointment.Id)
                .ToListAsync(cancellationToken);

            var dprofile = _db.DoctorProfiles.FirstOrDefault(p => p.Id == tps[0].DoctorID);

            var doctor = _userManager.FindByIdAsync(dprofile.DoctorId!).Result;
            foreach (var item in tps)
            {

                var sp = await _db.ServiceProcedures
                .Where(p => p.Id == item.ServiceProcedureId)
                .Select(b => new
                {
                    SP = b,
                    Procedure = _db.Procedures.FirstOrDefault(p => p.Id == b.ProcedureId),
                })
                .FirstOrDefaultAsync(cancellationToken);
                var r = new TreatmentPlanResponse
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
                };
                if (item.Status == Domain.Treatment.TreatmentPlanStatus.Active)
                {
                    r.StartDate = item.StartDate.Value;
                }
                result.Add(r);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<List<PrescriptionResponse>> GetPrescriptionByPatient(string id, CancellationToken cancellationToken)
    {
        try
        {
            var patientUser = await _userManager.FindByIdAsync(id);
            if(!await _userManager.IsInRoleAsync(patientUser, FSHRoles.Patient))
            {
                throw new Exception("User is not patient");
            }
            var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == patientUser.Id);
            var prescriptions = await _db.Prescriptions
                .Where(p => p.PatientID == patientProfile.Id)
                .Select(g => new
                {
                    Prescriptions = g,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == g.DoctorID),
                    Items = _db.PrescriptionItems
                        .Where(pi => pi.PrescriptionId == g.Id)
                        .ToList(),
                })
                .ToListAsync(cancellationToken);

            if (prescriptions == null)
            {
                throw new Exception("No prescriptions found for this patient.");
            }

            var result = new List<PrescriptionResponse>();

            foreach (var prescription in prescriptions)
            {
                var doctorUser = await _userManager.FindByIdAsync(prescription.Doctor.DoctorId);

                var prescriptionResponse = new PrescriptionResponse
                {
                    PatientID = patientUser.Id,
                    PatientName = $"{patientUser.FirstName} {patientUser.LastName}",
                    DoctorID = doctorUser.Id,
                    DoctorName = $"{doctorUser.FirstName} {doctorUser.LastName}",
                    Notes = prescription.Prescriptions.Notes,
                    Items = prescription.Items
                        .Where(i => i.PrescriptionId == prescription.Prescriptions.Id)
                        .Select(item => new PrescriptionItemRespomse
                        {
                            Dosage = item.Dosage,
                            Frequency = item.Frequency,
                            MedicineName = item.MedicineName
                        })
                        .ToList()
                };

                result.Add(prescriptionResponse);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PrescriptionResponse> GetPrescriptionByTreatment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _db.TreatmentPlanProcedures
                .Where(p => p.Id == id)
                .FirstOrDefaultAsync();

            if(plan == null)
            {
                throw new Exception("Plan can not be found");
            }
            if(plan.Status != Domain.Treatment.TreatmentPlanStatus.Completed)
            {
                throw new Exception("The plan do not have any prescription information.");
            }

            var pre = await _db.Prescriptions
                .Where(p => p.TreatmentID == plan.Id)
                .Select(s => new
                {
                    Prescription = s,
                    Patient = _db.PatientProfiles.FirstOrDefault(c => c.Id == s.PatientID),
                    Items = _db.PrescriptionItems.Where(p => p.PrescriptionId == s.Id).ToList(),
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == s.DoctorID),
                })
                .FirstOrDefaultAsync();

            if (pre != null) {
                var patient = await _userManager.FindByIdAsync(pre.Patient.UserId);
                var doctor = await _userManager.FindByIdAsync(pre.Doctor.DoctorId);
                var result = new PrescriptionResponse
                {
                    CreateDate = pre.Prescription.CreatedOn,
                    PatientID = patient.Id,
                    PatientName = patient.FirstName + " " + patient.LastName,
                    Notes = pre.Prescription.Notes,
                    DoctorID = doctor.Id,
                    DoctorName = doctor.FirstName + " " + doctor.LastName,
                };
                result.Items = new List<PrescriptionItemRespomse>();
                foreach(var item in pre.Prescription.Items)
                {
                    result.Items.Add(new PrescriptionItemRespomse
                    {
                        Dosage = item.Dosage,
                        Frequency = item.Frequency,
                        MedicineName = item.MedicineName,
                    });
                }
                return result;
            }
            else
            {
                throw new Exception("Prescription can not be found.");
            }

        }catch(Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }

    public async Task<List<TreatmentPlanResponse>> GetTreamentPlanByAppointment(Guid appointmentId, CancellationToken cancellationToken)
    {
        var result = new List<TreatmentPlanResponse>();
        try
        {
            var tps = await _db.TreatmentPlanProcedures
                .Where(p => p.AppointmentID == appointmentId)
                .ToListAsync(cancellationToken);
            foreach (var item in tps) {
                var dprofile = _db.DoctorProfiles.FirstOrDefault(p => p.Id == tps[0].DoctorID);
                var doctor = _userManager.FindByIdAsync(dprofile.DoctorId!).Result;
                var sp = await _db.ServiceProcedures
                .Where(p => p.Id == item.ServiceProcedureId)
                .Select(b => new
                {
                    SP = b,
                    Procedure = _db.Procedures.FirstOrDefault(p => p.Id == b.ProcedureId),
                })
                .FirstOrDefaultAsync(cancellationToken);
                var r = new TreatmentPlanResponse
                {
                    TreatmentPlanID = item.Id,
                    ProcedureID = sp.Procedure.Id,
                    ProcedureName = sp.Procedure.Name,
                    Price = sp.Procedure.Price,
                    DoctorID = doctor.Id,
                    DoctorName = $"{doctor.FirstName} {doctor.LastName}",
                    DiscountAmount = 0.3,
                    PlanCost = item.TotalCost,
                    PlanDescription = item.Note,
                    Step = sp.SP.StepOrder,
                    Status = item.Status,
                };
                if (item.Status != Domain.Treatment.TreatmentPlanStatus.Pending) {
                    r.StartDate = item.StartDate.Value;
                }
                result.Add(r);
            }
            result = result.OrderBy(result => result.Step).ToList();
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message, ex);
        }
        return result;
    }

    public async Task<string> UpdateTreamentPlan(AddTreatmentDetail request, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _db.TreatmentPlanProcedures
                .Where(p => p.Id == request.TreatmentId && p.AppointmentID == request.AppointmentID)
                .Select(b => new
                {
                    Plan = b,
                    SP = _db.ServiceProcedures.FirstOrDefault(p => p.Id == b.ServiceProcedureId)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if(plan == null)
            {
                throw new Exception("Can not find plan");
            }
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.AppointmentId == request.AppointmentID && p.PlanID == request.TreatmentId);
            if (calendar == null) {
                throw new Exception("The plan is not on calendar");
            }
            if(plan.SP.StepOrder != 1)
            {
                var past_procedure = await _db.ServiceProcedures
                    .Where(p => p.ServiceId == plan.SP.ServiceId && p.StepOrder == plan.SP.StepOrder - 1)
                    .FirstOrDefaultAsync();

                var past_plan = await _db.TreatmentPlanProcedures
                    .Where(p => p.ServiceProcedureId == past_procedure.Id && p.AppointmentID == request.AppointmentID)
                    .FirstOrDefaultAsync();

                var hasCalendar = await _db.WorkingCalendars
                    .FirstOrDefaultAsync(p => p.PlanID == past_plan.Id);

                if (hasCalendar == null)
                {
                    throw new Exception("The previous procedure is not done");
                }
                else
                {
                    if (request.TreatmentDate < hasCalendar.Date)
                    {
                        throw new Exception("Warning: the plan can not do when the previous plan in progress");
                    }
                }
            }
            calendar.Date = request.TreatmentDate;
            calendar.StartTime = request.TreatmentTime;
            calendar.EndTime = request.TreatmentTime.Add(TimeSpan.FromMinutes(30));
            plan.Plan.StartDate = request.TreatmentDate;
            plan.Plan.StartTime = request.TreatmentTime;

            await _db.SaveChangesAsync(cancellationToken);
            return _t["Success"];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message, ex);
        }
    }
}
