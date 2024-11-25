using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Auth;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Infrastructure.Treatments;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Query.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xceed.Words.NET;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FSH.WebApi.Infrastructure.Identity;

internal class WorkingCalendarService : IWorkingCalendarService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<WorkingCalendarService> _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WorkingCalendarService> _logger;

    public WorkingCalendarService(ApplicationDbContext db, IStringLocalizer<WorkingCalendarService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<WorkingCalendarService> logger)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> CheckAvailableTimeSlot(DateOnly date, TimeSpan start, TimeSpan end, string doctorId)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == doctorId);
        var calendars = await _db.WorkingCalendars
            .Where(c =>
                c.DoctorId == doctor.Id &&
                c.Date == date &&
                (
                    (c.StartTime <= start && start < c.EndTime) ||
                    (c.StartTime < end && end <= c.EndTime) ||
                    (start <= c.StartTime && c.EndTime <= end)
                ) &&
                (
                    c.Status == CalendarStatus.Booked ||
                    c.Status == CalendarStatus.Waiting
                )
            )
            .AnyAsync();

        return !calendars;
    }

    public async Task<bool> CheckAvailableTimeSlot(DateOnly date, TimeSpan start, TimeSpan end, Guid DoctorID)
    {
        var calendars = await _db.WorkingCalendars
            .Where(c =>
                c.DoctorId == DoctorID &&
                c.Date == date &&
                (
                    (c.StartTime <= start && start < c.EndTime) ||
                    (c.StartTime < end && end <= c.EndTime) ||
                    (start <= c.StartTime && c.EndTime <= end)
                ) &&
                (
                    c.Status == CalendarStatus.Booked ||
                    c.Status == CalendarStatus.Waiting
                )
            )
            .AnyAsync();

        return !calendars;
    }

    public async Task<bool> CheckAvailableTimeSlotToAddFollowUp(Guid doctorID, DateOnly treatmentDate, TimeSpan treatmentTime)
    {
        var endTime = treatmentTime.Add(TimeSpan.FromMinutes(30));
        var calendars = await _db.WorkingCalendars
            .Where(c =>
                c.DoctorId == doctorID &&
                c.Date == treatmentDate &&
                (
                    (c.StartTime <= treatmentTime && treatmentTime < c.EndTime) ||
                    (c.StartTime < endTime && endTime <= c.EndTime) ||
                    (treatmentTime <= c.StartTime && c.EndTime <= endTime)
                ) &&
                (
                    c.Status == CalendarStatus.Booked ||
                    c.Status == CalendarStatus.Waiting
                )
            )
            .AnyAsync();

        return !calendars;
    }

    public async Task<bool> CheckAvailableTimeSlotToReschedule(Guid appointmentID, DateOnly appointmentDate, TimeSpan startTime, TimeSpan endTime)
    {
        var existingCalendar = await _db.WorkingCalendars.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.AppointmentId == appointmentID) ?? throw new KeyNotFoundException("Calendar not found.");
        if (existingCalendar.Status != CalendarStatus.Booked) {
            return false;
        }
        var calendars = await _db.WorkingCalendars
            .Where(c =>
                c.DoctorId == existingCalendar.DoctorId &&
                c.Date == appointmentDate &&
                (
                    (c.StartTime <= startTime && startTime < c.EndTime) ||
                    (c.StartTime < endTime && endTime <= c.EndTime) ||
                    (startTime <= c.StartTime && c.EndTime <= endTime)
                ) &&
                (
                    c.Status == CalendarStatus.Booked ||
                    c.Status == CalendarStatus.Waiting
                )
            )
            .AnyAsync();

        return !calendars;
    }

    public List<WorkingCalendar> CreateWorkingCalendar(Guid doctorId, TimeSpan startTime, TimeSpan endTime, string? note = null)
    {
        var result = new List<WorkingCalendar>();

        var currentDate = DateOnly.FromDateTime(DateTime.Now);

        var lastDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, DateTime.DaysInMonth(currentDate.Year, currentDate.Month));
        var lastDate = DateOnly.FromDateTime(lastDayOfMonth);

        var date = currentDate;
        while (date <= lastDate)
        {
            var workingCalendar = new WorkingCalendar
            {
                DoctorId = doctorId,
                Date = date,
                StartTime = startTime,
                EndTime = endTime,
                Status = CalendarStatus.Waiting,
                Note = note,
                CreatedOn = DateTime.Now,
                CreatedBy = _currentUserService.GetUserId(),
            };

            result.Add(workingCalendar);
            date = date.AddDays(1);
        }

        return result;
    }

    public async Task<List<AvailableTimeResponse>> GetAvailableTimeSlot(GetAvailableTimeRequest request, CancellationToken cancellationToken)
    {
        var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DoctorID);
        var timeSlot = await _db.WorkingCalendars.Where(a => a.DoctorId == dprofile.Id && a.Date == request.Date &&
        (a.Status == CalendarStatus.Booked || a.Status == CalendarStatus.Waiting))
            .OrderBy(a => a.StartTime).ToListAsync();

        var startOfDay = new TimeSpan(8, 0, 0);
        var endOfDay = new TimeSpan(20, 0, 0);
        //var lunchStart = new TimeSpan(12, 0, 0);
        //var lunchEnd = new TimeSpan(14, 0, 0);

        var result = new List<AvailableTimeResponse>();

        for (var time = startOfDay; time < endOfDay; time += TimeSpan.FromMinutes(30))
        {
            //if (time >= lunchStart && time < lunchEnd)
            //{
            //    continue;
            //}
            result.Add(new AvailableTimeResponse
            {
                Time = time,
            });
        }
        foreach (var t in timeSlot)
        {
            result.RemoveAll(slot =>
                slot.Time >= t.StartTime && slot.Time < t.EndTime);
        }
        return result;
    }

    public async Task<GetWorkingDetailResponse> GetCalendarDetail(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            bool check = await _db.WorkingCalendars.AnyAsync(a => a.Id == id);
            if (!check)
            {
                throw new BadRequestException("Calendar Not Found");
            }
            var calendar = await _db.WorkingCalendars
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    Calendar = p,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == p.DoctorId),
                    Appointment = _db.Appointments.FirstOrDefault(a => a.Id == p.AppointmentId),
                    TreamentPlan = _db.TreatmentPlanProcedures.FirstOrDefault(t => t.Id == p.PlanID),
                })
                .FirstOrDefaultAsync();

            var pProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == calendar.Appointment.PatientId);
            var pUser = await _userManager.FindByIdAsync(pProfile.UserId);

            var dProfile = await _userManager.FindByIdAsync(calendar.Doctor.DoctorId);

            var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == calendar.Appointment.ServiceId);

            var result = new GetWorkingDetailResponse
            {
                CalendarID = calendar.Calendar.Id,
                PatientProfileID = calendar.Appointment.PatientId,
                PatientCode = pProfile.PatientCode,
                PatientName = pUser.UserName,
                DoctorProfileID = calendar.Doctor.Id,
                DoctorName = dProfile.UserName,
                AppointmentId = calendar.Appointment.Id,
                AppointmentType = calendar.Calendar.Type,
                ServiceID = service.Id,
                ServiceName = service.ServiceName,
                Date = calendar.Calendar.Date!.Value,
                StartTime = calendar.Calendar.StartTime!.Value,
                EndTime = calendar.Calendar.EndTime!.Value,
                Status = calendar.Calendar.Status,
                Note = calendar.Calendar.Note,
            };

            if(calendar.TreamentPlan != null)
            {
                var sp = await _db.ServiceProcedures
                .Where(p => p.Id == calendar.TreamentPlan.ServiceProcedureId)
                .Select(a => new
                {
                    SP = a,
                    Procedure = _db.Procedures.FirstOrDefault(r => r.Id == a.ProcedureId),
                })
                .FirstOrDefaultAsync();
                result.Step = sp.SP.StepOrder;
                result.ProcedureName = sp.Procedure.Name;
                result.ProcedureID = sp.Procedure.Id;
            }
            return result;

        }
        catch (Exception ex) {
            _logger.LogError(ex.Message, ex);
            throw new BadRequestException(ex.Message);
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetWorkingCalendars(PaginationFilter filter, DateOnly date, CancellationToken cancellation)
    {

        var result = new List<WorkingCalendarResponse>();
        int totalCount = 0;
        try
        {
            var currentUserRole = _currentUserService.GetRole();
            var currentUserId = _currentUserService.GetUserId().ToString();

            if (currentUserRole == FSHRoles.Dentist)
            {
                if (filter.AdvancedSearch == null)
                {
                    filter.AdvancedSearch = new Search();
                    filter.AdvancedSearch.Fields = new List<string>();
                }
                var profile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == currentUserId);
                filter.AdvancedSearch.Fields.Add("DoctorId");
                filter.AdvancedSearch.Keyword = profile.Id.ToString();
            }

            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);

            var query = _db.WorkingCalendars
            .Where(p => p.Status != CalendarStatus.Failed)
            .AsNoTracking();

            if (date != default)
            {
                query = query.Where(w => w.Date == date);
            }

            var calendarsGrouped = await query
                .Where(p => p.DoctorId != null)
                .WithSpecification(spec)
                .GroupBy(c => c.DoctorId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList());

            totalCount = calendarsGrouped.Count();

            foreach (var c in calendarsGrouped)
            {
                var dentist = c.Key;
                var calendars = c.Value;

                var appointment = await _db.Appointments
                        .FirstOrDefaultAsync(a => a.Id == calendars[0].AppointmentId, cancellation);

                if (appointment != null)
                {
                    var doc_profile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.Id == dentist);
                    var doc_infor = await _userManager.FindByIdAsync(doc_profile.DoctorId);
                    var service = await _db.Services.FirstOrDefaultAsync(p => p.Id == appointment.ServiceId);
                    if (doc_infor.IsActive)
                    {
                        var patientProfile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.Id == appointment.PatientId);
                        var patientUser = await _userManager.FindByIdAsync(patientProfile.UserId);
                        if (patientUser.IsActive)
                        {

                            result.Add(new WorkingCalendarResponse
                            {
                                DoctorProfileID = dentist.Value,
                                ImageUrl = doc_infor.ImageUrl,
                                UserName = $"{doc_infor.FirstName} {doc_infor.LastName}",
                                WorkingCalendars = calendars.Select(x =>
                                new WorkingCalendarDetail
                                {
                                    CalendarID = x.Id,
                                    AppointmentId = x.AppointmentId.Value,
                                    Date = x.Date.Value,
                                    EndTime = x.EndTime.Value,
                                    Note = x.Note,
                                    ServiceID = service.Id,
                                    ServiceName = service.ServiceName,
                                    PatientCode = patientProfile.PatientCode,
                                    PatientName = patientUser.UserName,
                                    PatientProfileID = patientProfile.Id,
                                    StartTime = x.StartTime.Value,
                                    Status = x.Status,
                                    AppointmentType = x.Type,
                                }).ToList(),
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
        }
        return new PaginationResponse<WorkingCalendarResponse>(
                result,
                totalCount,
                filter.PageNumber,
                filter.PageSize);
    }
}
