using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Wordprocessing;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
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
using FSH.WebApi.Infrastructure.Identity;
using static System.Runtime.InteropServices.JavaScript.JSType;
using FSH.WebApi.Domain.Payments;

namespace FSH.WebApi.Infrastructure.Appointments;

internal class AppointmentCalendarService : IAppointmentCalendarService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<AppointmentCalendarService> _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AppointmentCalendarService> _logger;

    public AppointmentCalendarService(ApplicationDbContext db, IStringLocalizer<AppointmentCalendarService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<AppointmentCalendarService> logger)
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
        var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == doctor.Id && p.Date == date && p.Status == WorkingStatus.Accept);
        if (workingTime == null)
        {
            return false;
        }
        else if (workingTime.Status != WorkingStatus.Accept)
        {
            return false;
        }
        bool time = await _db.TimeWorkings.Where(p =>
            p.CalendarID == workingTime.Id &&
            p.StartTime <= start &&
            p.EndTime >= end &&
            p.IsActive
        ).AnyAsync();

        if (!time)
        {
            return false;
        }
        bool calendars = await _db.AppointmentCalendars
            .Where(c =>
                c.DoctorId == doctor.Id &&
                c.Date == date &&
                (
                    c.StartTime <= start && start < c.EndTime ||
                    c.StartTime < end && end <= c.EndTime ||
                    start <= c.StartTime && c.EndTime <= end
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
        var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == DoctorID && p.Date == date && p.Status == WorkingStatus.Accept);
        if (workingTime == null)
        {
            return false;
        }
        else if (workingTime.Status != WorkingStatus.Accept) {
            return false;
        }
        bool time = await _db.TimeWorkings.Where(p =>
            p.CalendarID == workingTime.Id &&
            p.StartTime <= start &&
             p.EndTime >= end &&
            p.IsActive
        ).AnyAsync();

        if (!time)
        {
            return false;
        }

        bool calendars = await _db.AppointmentCalendars
            .Where(c =>
                c.DoctorId == DoctorID &&
                c.Date == date &&
                (
                    c.StartTime <= start && start < c.EndTime ||
                    c.StartTime < end && end <= c.EndTime ||
                    start <= c.StartTime && c.EndTime <= end
                ) &&
                (
                    c.Status == CalendarStatus.Booked ||
                    c.Status == CalendarStatus.Waiting
                )
            )
            .AnyAsync();

        return !calendars;
    }

    public async Task<bool> CheckAvailableTimeSlotForDash(DateOnly date, TimeSpan start, TimeSpan end, DefaultIdType DoctorID)
    {
        var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == DoctorID && p.Date == date && p.Status == WorkingStatus.Accept);
        if (workingTime == null)
        {
            return false;
        }
        else if (workingTime.Status != WorkingStatus.Accept)
        {
            return false;
        }
        bool time = await _db.TimeWorkings.Where(p =>
            p.CalendarID == workingTime.Id &&
            p.StartTime <= start &&
             p.EndTime >= end &&
            p.IsActive
        ).AnyAsync();

        if (!time)
        {
            return false;
        }
        return true;
    }

    public async Task<bool> CheckAvailableTimeSlotToAddFollowUp(DefaultIdType doctorID, DateOnly treatmentDate, TimeSpan treatmentTime)
    {
        var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.Id == doctorID);
        var endTime = treatmentTime.Add(TimeSpan.FromMinutes(30));
        var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == doctor.Id && p.Date == treatmentDate && p.Status == WorkingStatus.Accept);
        if (workingTime == null)
        {
            return false;
        }
        else if (workingTime.Status != WorkingStatus.Accept)
        {
            return false;
        }
        bool time = await _db.TimeWorkings.Where(p =>
            p.CalendarID == workingTime.Id &&
            p.StartTime <= treatmentTime &&
            p.EndTime >= endTime &&
            p.IsActive
        ).AnyAsync();

        if (!time)
        {
            return false;
        }
        bool calendars = await _db.AppointmentCalendars
            .Where(c =>
                c.DoctorId == doctorID &&
                c.Date == treatmentDate &&
                (
                    c.StartTime <= treatmentTime && treatmentTime < c.EndTime ||
                    c.StartTime < endTime && endTime <= c.EndTime ||
                    treatmentTime <= c.StartTime && c.EndTime <= endTime
                ) &&
                (
                    c.Status == CalendarStatus.Booked ||
                    c.Status == CalendarStatus.Waiting
                )
            )
            .AnyAsync();

        return !calendars;
    }

    public async Task<bool> CheckAvailableTimeSlotToReschedule(DefaultIdType appointmentID, DateOnly appointmentDate, TimeSpan startTime, TimeSpan endTime)
    {
        var existingCalendar = await _db.AppointmentCalendars.FirstOrDefaultAsync(p => p.AppointmentId == appointmentID) ?? throw new KeyNotFoundException("Calendar not found.");
        if (existingCalendar.Status != CalendarStatus.Booked)
        {
            return false;
        }

        var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == existingCalendar.DoctorId && p.Date == appointmentDate && p.Status != WorkingStatus.Off);
        if (workingTime == null)
        {
            return false;
        }
        else if (workingTime.Status != WorkingStatus.Accept)
        {
            return false;
        }
        bool time = await _db.TimeWorkings.Where(p =>
            p.CalendarID == workingTime.Id &&
            p.StartTime <= startTime &&
            p.EndTime >= endTime &&
            p.IsActive
        ).AnyAsync();

        if (!time)
        {
            return false;
        }
        bool calendars = await _db.AppointmentCalendars
            .Where(c =>
                c.DoctorId == existingCalendar.DoctorId &&
                c.Date == appointmentDate &&
                (
                    c.StartTime <= startTime && startTime < c.EndTime ||
                    c.StartTime < endTime && endTime <= c.EndTime ||
                    startTime <= c.StartTime && c.EndTime <= endTime
                ) &&
                (
                    c.Status == CalendarStatus.Booked ||
                    c.Status == CalendarStatus.Waiting
                )
            )
            .AnyAsync();

        return !calendars;
    }

    public List<AppointmentCalendar> CreateWorkingCalendar(DefaultIdType doctorId, TimeSpan startTime, TimeSpan endTime, string? note = null)
    {
        var result = new List<AppointmentCalendar>();

        var currentDate = DateOnly.FromDateTime(DateTime.Now);

        var lastDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, DateTime.DaysInMonth(currentDate.Year, currentDate.Month));
        var lastDate = DateOnly.FromDateTime(lastDayOfMonth);

        var date = currentDate;
        while (date <= lastDate)
        {
            var workingCalendar = new AppointmentCalendar
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
        var result = new List<AvailableTimeResponse>();
        var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.DoctorID);
        var workingTime = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == dprofile.Id && p.Date == request.Date && p.Status == WorkingStatus.Accept);
        if (workingTime == null)
        {
            return result;
        }
        else if (workingTime.Status != WorkingStatus.Accept)
        {
            return result;
        }
        var times = await _db.TimeWorkings.Where(p =>
            p.CalendarID == workingTime.Id &&
            p.IsActive
        ).ToListAsync();

        var timeSlot = await _db.AppointmentCalendars.Where(a => a.DoctorId == dprofile.Id && a.Date == request.Date &&
        (a.Status == CalendarStatus.Booked || a.Status == CalendarStatus.Waiting))
            .OrderBy(a => a.StartTime).ToListAsync();

        //var startOfDay = new TimeSpan(8, 0, 0);
        //var endOfDay = new TimeSpan(20, 0, 0);
        //var lunchStart = new TimeSpan(12, 0, 0);
        //var lunchEnd = new TimeSpan(13, 0, 0);

        foreach (var item in times) {
            for (var time = item.StartTime; time < item.EndTime; time += TimeSpan.FromMinutes(30))
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
        }
        foreach (var t in timeSlot)
        {
            result.RemoveAll(slot =>
                slot.Time > t.StartTime && slot.Time < t.EndTime);
        }
        result = result.OrderBy(p => p.Time).ToList();
        return result;
    }

    public async Task<GetWorkingDetailResponse> GetCalendarDetail(DefaultIdType id, CancellationToken cancellationToken)
    {
        try
        {
            bool check = await _db.AppointmentCalendars.AnyAsync(a => a.Id == id);
            if (!check)
            {
                throw new BadRequestException("Calendar Not Found");
            }
            var calendar = await _db.AppointmentCalendars
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
                PatientName = $"{pUser.FirstName} {pUser.LastName}",
                DoctorProfileID = calendar.Doctor.Id,
                DoctorName = $"{dProfile.FirstName} {dProfile.LastName}",
                AppointmentId = calendar.Appointment.Id,
                AppointmentType = calendar.Calendar.Type,
                ServiceID = service.Id,
                ServiceName = service.ServiceName,
                Date = calendar.Calendar.Date!.Value,
                StartTime = calendar.Calendar.StartTime!.Value,
                EndTime = calendar.Calendar.EndTime!.Value,
                Status = calendar.Calendar.Status,
                Note = calendar.Calendar.Note,
                PatientAvatar = pUser.ImageUrl,
                PatientPhone = pProfile.Phone,
                DoctorUserID = dProfile.Id,
            };

            if (calendar.TreamentPlan != null)
            {
                var sp = await _db.ServiceProcedures
                .Where(p => p.Id == calendar.TreamentPlan.ServiceProcedureId)
                .Select(a => new
                {
                    SP = a,
                    Procedure = _db.Procedures.FirstOrDefault(r => r.Id == a.ProcedureId),
                })
                .FirstOrDefaultAsync();
                result.TreatmentID = calendar.TreamentPlan.Id;
                result.Step = sp.SP.StepOrder;
                result.ProcedureName = sp.Procedure.Name;
                result.ProcedureID = sp.Procedure.Id;
            }
            var working = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == calendar.Doctor.Id && p.Date == calendar.Appointment.AppointmentDate && p.Status == WorkingStatus.Accept);

            if (working != null) {
                if (working.RoomID != default)
                {
                    var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == working.RoomID);
                    result.RoomID = room.Id;
                    result.RoomName = room.RoomName;
                }
            }
            return result;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new BadRequestException(ex.Message);
        }
    }

    public async Task<PaginationResponse<AppointmentCalendarResponse>> GetWorkingCalendars(PaginationFilter filter, DateOnly date, CancellationToken cancellation)
    {
        try
        {
            var result = new List<AppointmentCalendarResponse>();
            int totalCount = 0;
            string currentUserRole = _currentUserService.GetRole();
            string currentUserId = _currentUserService.GetUserId().ToString();

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

            var spec = new EntitiesByPaginationFilterSpec<AppointmentCalendar>(filter);

            var query = _db.AppointmentCalendars
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

                            result.Add(new AppointmentCalendarResponse
                            {
                                DoctorProfileID = dentist.Value,
                                ImageUrl = doc_infor.ImageUrl,
                                UserName = $"{doc_infor.FirstName} {doc_infor.LastName}",
                                WorkingCalendars = calendars.Select(x =>
                                new AppointmentCalendarDetail
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

            return new PaginationResponse<AppointmentCalendarResponse>(
                    result,
                    totalCount,
                    filter.PageNumber,
                    filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }
}
