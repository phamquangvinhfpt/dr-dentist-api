using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Wordprocessing;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Appointments;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Xceed.Document.NET;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FSH.WebApi.Infrastructure.Identity;
internal class WorkingCalendarService : IWorkingCalendarService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<WorkingCalendarService> _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WorkingCalendarService> _logger;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;
    public WorkingCalendarService(ApplicationDbContext db, IStringLocalizer<WorkingCalendarService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<WorkingCalendarService> logger, ICacheService cacheService, INotificationService notificationService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
        _cacheService = cacheService;
        _notificationService = notificationService;
    }

    public Task<bool> CheckAvailableTimeWorking(string DoctorID, DateOnly date, TimeSpan time)
    {
        throw new NotImplementedException();
    }

    public async Task<string> CreateWorkingCalendarForParttime(List<CreateOrUpdateWorkingCalendar> request, string doctorID, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var doctor = await _userManager.FindByIdAsync(doctorID);
            if (doctor == null) {
                throw new Exception("Waring: Doctor is not existing.");
            }
            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == doctorID);

            var weeklyHours = request
                .GroupBy(r => GetWeekOfMonth(r.Date))
                .ToDictionary(g => g.Key, g => 0);

            foreach (var item in request)
            {
                var existing = await _db.WorkingCalendars.AnyAsync(p => p.DoctorID == dProfile.Id && p.Date == DateOnly.FromDateTime(item.Date));
                if (existing) {
                    throw new Exception($"Date: {item.Date} has been taken in your working calendar");
                }
                int totalTimeInDay = 0;
                var calendar = _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
                {
                    DoctorID = dProfile.Id,
                    Date = DateOnly.FromDateTime(item.Date),
                    Status = WorkingStatus.Waiting,
                }).Entity;

                foreach (var t in item.TimeWorkings)
                {
                    int timeWorked = (int)(t.EndTime - t.StartTime).TotalHours;
                    if (timeWorked < 4)
                    {
                        throw new Exception($"At least 4 hours each session: {t.StartTime} - {t.EndTime}");
                    }
                    _db.TimeWorkings.Add(new Domain.Identity.TimeWorking
                    {
                        CalendarID = calendar.Id,
                        StartTime = t.StartTime,
                        EndTime = t.EndTime,
                        IsActive = true
                    });
                    totalTimeInDay += timeWorked;
                }

                if (dProfile.WorkingType == WorkingType.PartTime && totalTimeInDay < 4)
                {
                    throw new Exception($"Part-time doctor must work at least 4 hours per day. Date: {item.Date}");
                }

                // Add daily hours to weekly total
                int weekNumber = GetWeekOfMonth(item.Date);
                weeklyHours[weekNumber] += totalTimeInDay;
            }

            // Validate each week's total hours
            foreach (var weekHours in weeklyHours)
            {
                if (weekHours.Value != 20)
                {
                    throw new Exception($"Total time working for week {weekHours.Key} is {weekHours.Value} hours. Must be exactly 20 hours per week.");
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex) {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    private int GetWeekOfMonth(DateTime date)
    {
        return ISOWeek.GetWeekOfYear(date); ;
    }

    public async Task<string> FullTimeRegistDateWorking(string doctorID, DateTime date, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var doctor = await _userManager.FindByIdAsync(doctorID);
            if (doctor == null)
            {
                throw new Exception("Waring: Doctor is not existing.");
            }
            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == doctorID);

            var dayInMonth = DateTime.DaysInMonth(date.Year, date.Month);

            for (int day = date.Day; day <= dayInMonth; day++)
            {

                var currentDate = new DateOnly(date.Year, date.Month, day);

                var existing = await _db.WorkingCalendars.AnyAsync(p => p.DoctorID == dProfile.Id && p.Date == currentDate);
                if (existing) {
                    throw new Exception($"Day: {currentDate} has been existing");
                }

                // Skip only Sundays
                if (currentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }
                // Create working calendar for workday
                var calendar = _db.WorkingCalendars.Add(new Domain.Identity.WorkingCalendar
                {
                    DoctorID = dProfile.Id,
                    Date = currentDate,
                    Status = WorkingStatus.Waiting,
                }).Entity;
                //var time = _db.TimeWorkings.Add(new TimeWorking
                //{
                //    CalendarID = calendar.Id,
                //    IsActive = false
                //});
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<string> UpdateWorkingCalendar(List<CreateOrUpdateWorkingCalendar> request, string doctorID, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var doctor = await _userManager.FindByIdAsync(doctorID);
            if (doctor == null)
            {
                throw new Exception("Waring: Doctor is not existing.");
            }
            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == doctorID);

            foreach (var item in request)
            {
                int totalTimeInDay = 0;

                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == dProfile.Id && p.Date == DateOnly.FromDateTime(item.Date));
                if (calendar == null) {
                    //throw new Exception("Warning: Error when find calendar");
                    _logger.LogInformation("Warning: Error when find calendar at UpdateWorkingCalendar. To Processing create new working time.");
                }
                if(calendar.Status != WorkingStatus.Waiting)
                {
                    throw new Exception("Warning: The time was accept or cancel");
                }
                foreach (var t in item.TimeWorkings)
                {
                    int timeWorked = (int)(t.EndTime - t.StartTime).TotalHours;
                    //if (timeWorked < 4 && dProfile.WorkingType == WorkingType.PartTime)
                    //{
                    //    throw new Exception($"At least 4 hour each session for part time doctor: {t.StartTime} - {t.EndTime}");
                    //}

                    if(dProfile.WorkingType == WorkingType.PartTime)
                    {
                        var times = await _db.TimeWorkings.Where(p => p.CalendarID == calendar.Id).ToListAsync();
                        if (times.Count() == 0)
                        {
                            throw new Exception("Warning: Error when find time working");
                        }
                        foreach (var time in times)
                        {
                            time.StartTime = t.StartTime;
                            time.EndTime = t.EndTime;
                            time.LastModifiedBy = _currentUserService.GetUserId();
                        }
                    }
                    else
                    {
                        _db.TimeWorkings.Add(new TimeWorking
                        {
                            CalendarID = calendar.Id,
                            StartTime = t.StartTime,
                            EndTime = t.EndTime,
                            IsActive = true
                        });
                    }

                    totalTimeInDay += timeWorked;
                }
                if (dProfile.WorkingType == WorkingType.PartTime && totalTimeInDay < 4)
                {
                    throw new Exception($"Part-time doctor must work at least 4 hours per day. Date: {item.Date}");
                }else if (dProfile.WorkingType == WorkingType.FullTime && totalTimeInDay < 8)
                {
                    throw new Exception($"Fulltime doctor must work at 8 hours per day. Date: {item.Date}");
                }
                calendar.Status = dProfile.WorkingType == WorkingType.FullTime ? WorkingStatus.Accept : WorkingStatus.Waiting;
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetWorkingCalendarPagination(PaginationFilter filter, DateOnly date, DateOnly Edate,  CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<WorkingCalendarResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);
            var query = _db.WorkingCalendars
                .AsNoTracking().Where(p => p.Status == WorkingStatus.Accept);

            if(currentUser == FSHRoles.Dentist)
            {
                string id = _currentUserService.GetUserId().ToString();
                var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == id);
                query = query.Where(p => p.DoctorID == doctor.Id);
            }

            if (date != default)
            {
                query = query.Where(w => w.Date >= date);
            }
            if (Edate != default)
            {
                query = query.Where(w => w.Date <= Edate);
            }
            int count = query.Count();

            query = query.OrderBy(p => p.Date);

            var calendars = await query.WithSpecification(spec)
                .GroupBy(p => p.DoctorID)
                .Select(c => new
                {
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.Key),
                    Calendar = c.ToList(),
                })
                .ToListAsync();
            foreach (var calendar in calendars)
            {
                var u = await _userManager.FindByIdAsync(calendar.Doctor.DoctorId);
                var r = new WorkingCalendarResponse
                {
                    DentistUserID = u.Id,
                    DentistProfileId = calendar.Doctor.Id,
                    DentistName = $"{u.FirstName} {u.LastName}",
                    DentistImage = u.ImageUrl,
                    Phone = u.PhoneNumber,
                    WorkingType = calendar.Doctor.WorkingType,
                };
                r.CalendarDetails = new List<CalendarDetail>();
                foreach(var item in calendar.Calendar)
                {
                    var t = new CalendarDetail
                    {
                        CalendarID = item.Id,
                        Date = item.Date.Value,
                        Note = item.Note,
                        WorkingStatus = item.Status,
                    };
                    if (item.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == item.RoomID);
                        t.RoomID = room.Id;
                        t.RoomName = room.RoomName;
                    }
                    t.Times = new List<TimeDetail>();
                    var times = await _db.TimeWorkings.Where(p => p.CalendarID == item.Id).ToListAsync();
                    foreach(var i in times)
                    {
                        t.Times.Add(new TimeDetail
                        {
                            TimeID = i.Id,
                            EndTime = i.EndTime,
                            IsActive = i.IsActive,
                            StartTime = i.StartTime,
                        });
                    }
                    r.CalendarDetails.Add(t);
                }
                result.Add(r);
            }
            return new PaginationResponse<WorkingCalendarResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<string> AddRoomForWorkingAsync(AddRoomToWorkingRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == request.RoomID);
            if (room == null) {
                throw new Exception("Warning: Error when find room");
            }
            var calendar = await _db.WorkingCalendars
                .Where(p => p.Id == request.CalendarID)
                .Select(c => new
                {
                    Calendar = c,
                    Times = _db.TimeWorkings.Where(p => p.CalendarID == c.Id).ToList(),
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.DoctorID)
                })
                .FirstOrDefaultAsync();
            if (calendar == null) {
                throw new Exception("Warning: Error when find calendar");
            }
            if (calendar.Times.Count() == 0)
            {
                throw new Exception("Warning: Time was not selected.");
            }
            if (calendar.Doctor.WorkingType == WorkingType.FullTime && calendar.Calendar.Status != WorkingStatus.Accept)
            {
                throw new Exception("Warning: Time working that is not set to doctor full time.");
            }
            var wasUse = await _db.WorkingCalendars
                .Where(p => p.RoomID == request.RoomID && p.Date == calendar.Calendar.Date && p.Status == WorkingStatus.Accept)
                .ToListAsync();
            bool flag = false;
            foreach (var item in wasUse) {
                foreach (var item2 in calendar.Times) {
                    flag = await _db.TimeWorkings.AnyAsync(c =>
                        c.CalendarID == item.Id && (
                        c.StartTime < item2.StartTime && item2.StartTime < c.EndTime ||
                        c.StartTime < item2.EndTime && item2.EndTime < c.EndTime ||
                        item2.EndTime <= c.StartTime && c.EndTime <= item2.EndTime
                    ));
                    if (flag) {
                        throw new Exception($"Warning: the room has been taken at {calendar.Calendar.Date} {item2.StartTime} - {item2.EndTime}");
                    }
                }
            }
            if (calendar.Doctor.WorkingType == WorkingType.PartTime) {
                calendar.Calendar.Status = WorkingStatus.Accept;
                foreach (var item in calendar.Times) {
                    item.IsActive = true;
                }
            }
            calendar.Calendar.RoomID = request.RoomID;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<string> CreateRoomsAsync(AddRoomRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await _db.Rooms.AnyAsync(p => p.RoomName == request.Name);
            if (existing) {
                throw new Exception("The room is existing");
            }
            _db.Rooms.Add(new Domain.Examination.Room
            {
                RoomName = request.Name,
                Status = true
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PaginationResponse<Room>> GetRoomsWithPagination(PaginationFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            var spec = new EntitiesByPaginationFilterSpec<Room>(filter);
            var query = await _db.Rooms
                .AsNoTracking().WithSpecification(spec).ToListAsync();
            int count = _db.Rooms.Count();

            return new PaginationResponse<Room>(query, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetPartTimeNonAcceptWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<WorkingCalendarResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);
            var query = _db.WorkingCalendars
                .AsNoTracking().Where(p => p.Status == WorkingStatus.Waiting);

            if (currentUser == FSHRoles.Dentist)
            {
                string id = _currentUserService.GetUserId().ToString();
                var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == id);
                query = query.Where(p => p.DoctorID == doctor.Id);
            }
            else
            {
                var ptDoctor = await _db.DoctorProfiles.Where(p => p.WorkingType == WorkingType.PartTime).Select(p => p.Id).ToListAsync();
                query = query.Where(p => ptDoctor.Contains(p.DoctorID));
            }

            if (startDate != default)
            {
                query = query.Where(w => w.Date >= startDate);
            }
            if (endDate != default)
            {
                query = query.Where(w => w.Date <= endDate);
            }
            int count = query.Count();

            query = query.OrderBy(p => p.Date);

            var calendars = await query.WithSpecification(spec)
                .GroupBy(p => p.DoctorID)
                .Select(c => new
                {
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.Key),
                    Calendar = c.ToList(),
                })
                .ToListAsync();
            foreach (var calendar in calendars)
            {
                var u = await _userManager.FindByIdAsync(calendar.Doctor.DoctorId);
                var r = new WorkingCalendarResponse
                {
                    DentistUserID = u.Id,
                    DentistProfileId = calendar.Doctor.Id,
                    DentistName = $"{u.FirstName} {u.LastName}",
                    DentistImage = u.ImageUrl,
                    Phone = u.PhoneNumber,
                    WorkingType = calendar.Doctor.WorkingType,
                };
                r.CalendarDetails = new List<CalendarDetail>();
                foreach (var item in calendar.Calendar)
                {
                    var t = new CalendarDetail
                    {
                        CalendarID = item.Id,
                        Date = item.Date.Value,
                        Note = item.Note,
                        WorkingStatus = item.Status,
                    };
                    //if (item.RoomID != default)
                    //{
                    //    var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == item.RoomID);
                    //    t.RoomID = room.Id;
                    //    t.RoomName = room.RoomName;
                    //}
                    t.Times = new List<TimeDetail>();
                    var times = await _db.TimeWorkings.Where(p => p.CalendarID == item.Id).ToListAsync();
                    foreach (var i in times)
                    {
                        t.Times.Add(new TimeDetail
                        {
                            TimeID = i.Id,
                            EndTime = i.EndTime,
                            IsActive = i.IsActive,
                            StartTime = i.StartTime,
                        });
                    }
                    r.CalendarDetails.Add(t);
                }
                result.Add(r);
            }
            return new PaginationResponse<WorkingCalendarResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetFullTimeNonAcceptWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<WorkingCalendarResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);
            var query = _db.WorkingCalendars
                .AsNoTracking().Where(p => p.Status == WorkingStatus.Waiting);

            if (currentUser == FSHRoles.Dentist)
            {
                string id = _currentUserService.GetUserId().ToString();
                var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == id);
                query = query.Where(p => p.DoctorID == doctor.Id);
            }
            else
            {
                var ptDoctor = await _db.DoctorProfiles.Where(p => p.WorkingType == WorkingType.FullTime).Select(p => p.Id).ToListAsync();
                query = query.Where(p => ptDoctor.Contains(p.DoctorID));
            }

            if (startDate != default)
            {
                query = query.Where(w => w.Date >= startDate);
            }
            if (endDate != default)
            {
                query = query.Where(w => w.Date <= endDate);
            }
            int count = query.Count();

            query = query.OrderBy(p => p.Date);

            var calendars = await query.WithSpecification(spec)
                .GroupBy(p => p.DoctorID)
                .Select(c => new
                {
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.Key),
                    Calendar = c.ToList(),
                })
                .ToListAsync();
            foreach (var calendar in calendars)
            {
                var u = await _userManager.FindByIdAsync(calendar.Doctor.DoctorId);
                var r = new WorkingCalendarResponse
                {
                    DentistUserID = u.Id,
                    DentistProfileId = calendar.Doctor.Id,
                    DentistName = $"{u.FirstName} {u.LastName}",
                    DentistImage = u.ImageUrl,
                    Phone = u.PhoneNumber,
                    WorkingType = calendar.Doctor.WorkingType,
                };
                r.CalendarDetails = new List<CalendarDetail>();
                foreach (var item in calendar.Calendar)
                {
                    var t = new CalendarDetail
                    {
                        CalendarID = item.Id,
                        Date = item.Date.Value,
                        Note = item.Note,
                        WorkingStatus = item.Status,
                    };
                    //if (item.RoomID != default)
                    //{
                    //    var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == item.RoomID);
                    //    t.RoomID = room.Id;
                    //    t.RoomName = room.RoomName;
                    //}
                    t.Times = new List<TimeDetail>();
                    var times = await _db.TimeWorkings.Where(p => p.CalendarID == item.Id).ToListAsync();
                    foreach (var i in times)
                    {
                        t.Times.Add(new TimeDetail
                        {
                            TimeID = i.Id,
                            EndTime = i.EndTime,
                            IsActive = i.IsActive,
                            StartTime = i.StartTime,
                        });
                    }
                    r.CalendarDetails.Add(t);
                }
                result.Add(r);
            }
            return new PaginationResponse<WorkingCalendarResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetFullTimeOffWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<WorkingCalendarResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);

            var query = _db.WorkingCalendars
                .AsNoTracking().Where(p => p.Status == WorkingStatus.Off);

            if (currentUser == FSHRoles.Dentist)
            {
                string id = _currentUserService.GetUserId().ToString();
                var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == id);
                query = query.Where(p => p.DoctorID == doctor.Id);
            }
            else
            {
                var ptDoctor = await _db.DoctorProfiles.Where(p => p.WorkingType == WorkingType.FullTime).Select(p => p.Id).ToListAsync();
                query = query.Where(p => ptDoctor.Contains(p.DoctorID));
            }

            if (startDate != default)
            {
                query = query.Where(w => w.Date >= startDate);
            }
            if (endDate != default)
            {
                query = query.Where(w => w.Date <= endDate);
            }
            int count = query.Count();

            query = query.OrderBy(p => p.Date);

            var calendars = await query.WithSpecification(spec)
                .GroupBy(p => p.DoctorID)
                .Select(c => new
                {
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.Key),
                    Calendar = c.ToList(),
                })
                .ToListAsync();
            foreach (var calendar in calendars)
            {
                var u = await _userManager.FindByIdAsync(calendar.Doctor.DoctorId);
                var r = new WorkingCalendarResponse
                {
                    DentistUserID = u.Id,
                    DentistProfileId = calendar.Doctor.Id,
                    DentistName = $"{u.FirstName} {u.LastName}",
                    DentistImage = u.ImageUrl,
                    Phone = u.PhoneNumber,
                    WorkingType = calendar.Doctor.WorkingType,
                };
                r.CalendarDetails = new List<CalendarDetail>();
                foreach (var item in calendar.Calendar)
                {
                    var t = new CalendarDetail
                    {
                        CalendarID = item.Id,
                        Date = item.Date.Value,
                        Note = item.Note,
                        WorkingStatus = item.Status,
                    };
                    if (item.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == item.RoomID);
                        t.RoomID = room.Id;
                        t.RoomName = room.RoomName;
                    }
                    t.Times = new List<TimeDetail>();
                    var times = await _db.TimeWorkings.Where(p => p.CalendarID == item.Id).ToListAsync();
                    foreach (var i in times)
                    {
                        t.Times.Add(new TimeDetail
                        {
                            TimeID = i.Id,
                            EndTime = i.EndTime,
                            IsActive = i.IsActive,
                            StartTime = i.StartTime,
                        });
                    }
                    r.CalendarDetails.Add(t);
                }
                result.Add(r);
            }
            return new PaginationResponse<WorkingCalendarResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetPartTimeOffWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<WorkingCalendarResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);

            var query = _db.WorkingCalendars
                .AsNoTracking().Where(p => p.Status == WorkingStatus.Off);

            if (currentUser == FSHRoles.Dentist)
            {
                string id = _currentUserService.GetUserId().ToString();
                var doctor = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == id);
                query = query.Where(p => p.DoctorID == doctor.Id);
            }
            else
            {
                var ptDoctor = await _db.DoctorProfiles.Where(p => p.WorkingType == WorkingType.PartTime).Select(p => p.Id).ToListAsync();
                query = query.Where(p => ptDoctor.Contains(p.DoctorID));
            }

            if (startDate != default)
            {
                query = query.Where(w => w.Date >= startDate);
            }
            if (endDate != default)
            {
                query = query.Where(w => w.Date <= endDate);
            }
            int count = query.Count();

            query = query.OrderBy(p => p.Date);

            var calendars = await query.WithSpecification(spec)
                .GroupBy(p => p.DoctorID)
                .Select(c => new
                {
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.Key),
                    Calendar = c.ToList(),
                })
                .ToListAsync();
            foreach (var calendar in calendars)
            {
                var u = await _userManager.FindByIdAsync(calendar.Doctor.DoctorId);
                var r = new WorkingCalendarResponse
                {
                    DentistUserID = u.Id,
                    DentistProfileId = calendar.Doctor.Id,
                    DentistName = $"{u.FirstName} {u.LastName}",
                    DentistImage = u.ImageUrl,
                    Phone = u.PhoneNumber,
                    WorkingType = calendar.Doctor.WorkingType,
                };
                r.CalendarDetails = new List<CalendarDetail>();
                foreach (var item in calendar.Calendar)
                {
                    var t = new CalendarDetail
                    {
                        CalendarID = item.Id,
                        Date = item.Date.Value,
                        Note = item.Note,
                        WorkingStatus = item.Status,
                    };
                    if (item.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == item.RoomID);
                        t.RoomID = room.Id;
                        t.RoomName = room.RoomName;
                    }
                    t.Times = new List<TimeDetail>();
                    var times = await _db.TimeWorkings.Where(p => p.CalendarID == item.Id).ToListAsync();
                    foreach (var i in times)
                    {
                        t.Times.Add(new TimeDetail
                        {
                            TimeID = i.Id,
                            EndTime = i.EndTime,
                            IsActive = i.IsActive,
                            StartTime = i.StartTime,
                        });
                    }
                    r.CalendarDetails.Add(t);
                }
                result.Add(r);
            }
            return new PaginationResponse<WorkingCalendarResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetAllNonAcceptWithPagination(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<WorkingCalendarResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);
            var query = _db.WorkingCalendars
                .AsNoTracking().Where(p => p.Status == WorkingStatus.Waiting);
            if (startDate != default)
            {
                query = query.Where(w => w.Date >= startDate);
            }
            if (endDate != default)
            {
                query = query.Where(w => w.Date <= endDate);
            }
            int count = query.Count();

            query = query.OrderBy(p => p.Date);

            var calendars = await query.WithSpecification(spec)
                .GroupBy(p => p.DoctorID)
                .Select(c => new
                {
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.Key),
                    Calendar = c.ToList(),
                })
                .ToListAsync();
            foreach (var calendar in calendars)
            {
                var u = await _userManager.FindByIdAsync(calendar.Doctor.DoctorId);
                var r = new WorkingCalendarResponse
                {
                    DentistUserID = u.Id,
                    DentistProfileId = calendar.Doctor.Id,
                    DentistName = $"{u.FirstName} {u.LastName}",
                    DentistImage = u.ImageUrl,
                    Phone = u.PhoneNumber,
                    WorkingType = calendar.Doctor.WorkingType,
                };
                r.CalendarDetails = new List<CalendarDetail>();
                foreach (var item in calendar.Calendar)
                {
                    var t = new CalendarDetail
                    {
                        CalendarID = item.Id,
                        Date = item.Date.Value,
                        Note = item.Note,
                        WorkingStatus = item.Status,
                    };
                    if (item.RoomID != default)
                    {
                        var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == item.RoomID);
                        t.RoomID = room.Id;
                        t.RoomName = room.RoomName;
                    }
                    t.Times = new List<TimeDetail>();
                    var times = await _db.TimeWorkings.Where(p => p.CalendarID == item.Id).ToListAsync();
                    foreach (var i in times)
                    {
                        t.Times.Add(new TimeDetail
                        {
                            TimeID = i.Id,
                            EndTime = i.EndTime,
                            IsActive = i.IsActive,
                            StartTime = i.StartTime,
                        });
                    }
                    r.CalendarDetails.Add(t);
                }
                result.Add(r);
            }
            return new PaginationResponse<WorkingCalendarResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    public async Task<List<GetDoctorResponse>> GetAllDoctorHasNonCalendar(DateTime date, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<GetDoctorResponse>();
            var targetMonth = date.Month;
            var targetYear = date.Year;

            // Lấy danh sách doctors chưa có lịch trong tháng specified
            var doctorsWithNoSchedule = await _db.DoctorProfiles
                .Where(d => !_db.WorkingCalendars.Any(w =>
                    w.DoctorID == d.Id &&
                    w.Date.Value.Month == targetMonth &&
                    w.Date.Value.Year == targetYear))
                .ToListAsync(cancellationToken);

            foreach(var doctor in doctorsWithNoSchedule)
            {
                var user = await _userManager.FindByIdAsync(doctor.DoctorId);
                result.Add(new GetDoctorResponse
                {
                    DoctorProfile = doctor,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    Gender = user.Gender,
                    Id = user.Id,
                    ImageUrl = user.ImageUrl,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    UserName = user.UserName,
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting doctors with no calendar: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<List<TimeDetail>> GetTimeWorkingAsync(GetTimeWorkingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if(request.UserID == null)
            {
                throw new Exception("Warning: user information should be include.");
            }
            if(request.Date == default)
            {
                throw new Exception("Warning: The Date is default.");
            }

            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.UserID);

            if (dProfile == null) {
                throw new Exception("Warning: Can not identify doctor.");
            }
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == dProfile.Id && p.Date == request.Date);
            if(calendar == null)
            {
                throw new Exception("Warning: User do not have work at this day.");
            }
            var times = await _db.TimeWorkings.Where(p => p.CalendarID == calendar.Id)
                .Select(c => new TimeDetail
                {
                    EndTime = c.EndTime,
                    StartTime = c.StartTime,
                    TimeID = c.Id,
                    IsActive = c.IsActive,
                }).ToListAsync();
            return times;
        }
        catch (Exception ex) {
            _logger.LogError("Error getting doctors with no calendar: {Message}", ex.Message);
            throw new Exception(ex.Message);
        }
    }
}
