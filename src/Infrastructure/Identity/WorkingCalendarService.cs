using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Identity;

internal class WorkingCalendarService : IWorkingCalendarService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<WorkingCalendarService> _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkingCalendarService(ApplicationDbContext db, IStringLocalizer<WorkingCalendarService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
    }

    public async Task<bool> CheckAvailableTimeSlot(DateOnly date, TimeSpan start, TimeSpan end, Guid doctorId)
    {
        var calendars = await _db.WorkingCalendars
            .Where(c =>
                c.DoctorId == doctorId &&
                c.Date == date &&
                (
                    (c.StartTime <= start && start < c.EndTime) ||
                    (c.StartTime < end && end <= c.EndTime) ||
                    (start <= c.StartTime && c.EndTime <= end)
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

    public List<WorkingCalendarResponse> GetWorkingCalendars(CancellationToken cancellation)
    {
        var doctors = _db.DoctorProfiles.ToList();
        var result = new List<WorkingCalendarResponse>();
        foreach (var doctor in doctors)
        {
            var user = _userManager.FindByIdAsync(doctor.DoctorId).Result;
            var schedules = _db.WorkingCalendars.Where(p => p.DoctorId == doctor.Id).ToList() ?? null;
            result.Add(new WorkingCalendarResponse
            {
                DoctorProfileID = doctor.Id,
                ImageUrl = user.ImageUrl,
                UserName = user.UserName,
                WorkingCalendars = schedules,
            });
        }
        return result;
    }
}
