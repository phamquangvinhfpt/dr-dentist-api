using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Wordprocessing;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Exporters;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Infrastructure.Appointments;
using FSH.WebApi.Infrastructure.Auditing;
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
    private readonly IExcelWriter _excelWriter;
    private readonly IAppointmentService _appointmentService;

    public WorkingCalendarService(ApplicationDbContext db, IStringLocalizer<WorkingCalendarService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, ILogger<WorkingCalendarService> logger, ICacheService cacheService, INotificationService notificationService, IExcelWriter excelWriter, IAppointmentService appointmentService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
        _cacheService = cacheService;
        _notificationService = notificationService;
        _excelWriter = excelWriter;
        _appointmentService = appointmentService;
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
            if (doctor == null)
            {
                throw new Exception("Waring: Doctor is not existing.");
            }
            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == doctorID);

            var weeklyHours = request
            .GroupBy(r => new { Month = r.Date.Month, Week = GetWeekOfMonth(r.Date) })
            .ToDictionary(g => g.Key, g => new WeeklyData
            {
                Hours = 0,
                FirstDate = g.Min(x => x.Date),
                LastDate = g.Max(x => x.Date),
                WorkDays = g.Count()
            });

            foreach (var item in request)
            {
                var existing = await _db.WorkingCalendars.AnyAsync(p => p.DoctorID == dProfile.Id && p.Date == DateOnly.FromDateTime(item.Date));
                if (existing)
                {
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
                var key = new { Month = item.Date.Month, Week = GetWeekOfMonth(item.Date) };
                weeklyHours[key].Hours += totalTimeInDay;
            }

            // Validate each week's total hours
            foreach (var weekHours in weeklyHours)
            {
                if (weekHours.Key.Week == 1 && weekHours.Value.FirstDate.DayOfWeek > DayOfWeek.Tuesday)
                {
                    continue;
                }
                if (weekHours.Value.Hours < 20)
                {
                    throw new Exception($"Total time working for week {weekHours.Key.Week} in month {weekHours.Key.Month} " +
                    $"is {weekHours.Value.Hours} hours. Must be at least 20 hours for {weekHours.Value.WorkDays} working days.");
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    private int GetWeekOfMonth(DateTime date)
    {
        return ISOWeek.GetWeekOfYear(date);
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

                bool existing = await _db.WorkingCalendars.AnyAsync(p => p.DoctorID == dProfile.Id && p.Date == currentDate);
                if (existing)
                {
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
                if (calendar == null)
                {
                    //throw new Exception("Warning: Error when find calendar");
                    _logger.LogInformation("Warning: Error when find calendar at UpdateWorkingCalendar. To Processing create new working time.");
                }
                if (calendar.Status != WorkingStatus.Waiting)
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

                    if (dProfile.WorkingType == WorkingType.PartTime)
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
                }
                else if (dProfile.WorkingType == WorkingType.FullTime && totalTimeInDay < 8)
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
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<WorkingCalendarResponse>> GetWorkingCalendarPagination(PaginationFilter filter, DateOnly date, DateOnly Edate, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<WorkingCalendarResponse>();
            var spec = new EntitiesByPaginationFilterSpec<WorkingCalendar>(filter);
            var query = _db.WorkingCalendars
                .AsNoTracking().Where(p => p.Status == WorkingStatus.Accept);

            if (currentUser == FSHRoles.Dentist)
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
                    t.Times = t.Times.OrderBy(p => p.StartTime).ToList();
                    r.CalendarDetails.Add(t);
                }
                r.CalendarDetails = r.CalendarDetails.OrderBy(p => p.Date).ToList();
                result.Add(r);
            }
            return new PaginationResponse<WorkingCalendarResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> AddRoomForWorkingAsync(AddRoomToWorkingRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == request.RoomID);
            if (room == null)
            {
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
            if (calendar == null)
            {
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
            foreach (var item in wasUse)
            {
                foreach (var item2 in calendar.Times)
                {
                    flag = await _db.TimeWorkings.AnyAsync(c =>
                        c.CalendarID == item.Id && (
                        c.StartTime <= item2.StartTime && item2.StartTime < c.EndTime ||
                        c.StartTime < item2.EndTime && item2.EndTime <= c.EndTime ||
                        item2.StartTime <= c.StartTime && c.EndTime <= item2.EndTime
                    ));
                    if (flag)
                    {
                        throw new Exception($"Warning: the room has been taken at {calendar.Calendar.Date} {item2.StartTime} - {item2.EndTime}");
                    }
                }
            }
            if (calendar.Doctor.WorkingType == WorkingType.PartTime)
            {
                calendar.Calendar.Status = WorkingStatus.Accept;
                foreach (var item in calendar.Times)
                {
                    item.IsActive = true;
                }
            }
            calendar.Calendar.RoomID = request.RoomID;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await _appointmentService.DeleteRedisCode();
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> CreateRoomsAsync(AddRoomRequest request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await _db.Rooms.AnyAsync(p => p.RoomName == request.Name);
            if (existing)
            {
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
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<RoomDetail>> GetRoomsWithPagination(PaginationFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<RoomDetail>();
            var spec = new EntitiesByPaginationFilterSpec<Room>(filter);
            var query = await _db.Rooms
                .AsNoTracking().WithSpecification(spec).ToListAsync();
            int count = _db.Rooms.Count();

            var date = DateTime.Now;
            var time = date.TimeOfDay;

            foreach (var item in query)
            {
                var r = new RoomDetail
                {
                    CreateDate = DateOnly.FromDateTime(item.CreatedOn),
                    RoomID = item.Id,
                    RoomName = item.RoomName,
                };
                var isUse = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.RoomID == item.Id &&
                    p.Status == WorkingStatus.Accept && p.Date == DateOnly.FromDateTime(date) &&
                    _db.TimeWorkings.Any(t => t.CalendarID == p.Id &&
                    t.StartTime <= time && t.EndTime >= time && t.IsActive));
                if (isUse != null)
                {
                    var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.Id == isUse.DoctorID);
                    var doctor = await _userManager.FindByIdAsync(dProfile.DoctorId);
                    r.DoctorID = doctor.Id;
                    r.DoctorName = $"{doctor.FirstName} {doctor.LastName}";
                    r.Status = true;
                    item.Status = true;
                }
                else
                {
                    item.Status = false;
                }
                result.Add(r);
            }
            await _db.SaveChangesAsync(cancellationToken);
            return new PaginationResponse<RoomDetail>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
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
            throw new Exception(ex.Message);
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
            throw new Exception(ex.Message);
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
            throw new Exception(ex.Message);
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
            throw new Exception(ex.Message);
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
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<GetDoctorResponse>> GetAllDoctorHasNonCalendar(DateTime date, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<GetDoctorResponse>();
            var targetMonth = date.Month;
            var targetYear = date.Year;

            var doctorsWithNoSchedule = await _db.DoctorProfiles
                .Where(d => !_db.WorkingCalendars.Any(w =>
                    w.DoctorID == d.Id &&
                    w.Date.Value.Month == targetMonth &&
                    w.Date.Value.Year == targetYear))
                .ToListAsync(cancellationToken);

            foreach (var doctor in doctorsWithNoSchedule)
            {
                var user = await _userManager.FindByIdAsync(doctor.DoctorId);
                if (user.IsActive)
                {
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
            }
            var startOfMonth = new DateOnly(targetYear, targetMonth, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            var partTimeDoctors = await _db.DoctorProfiles
            .Where(p => p.WorkingType == WorkingType.PartTime &&
                _db.WorkingCalendars.Any(w =>
                    w.DoctorID == p.Id &&
                    w.Date.Value.Month == targetMonth &&
                    w.Date.Value.Year == targetYear))
            .Select(d => new
            {
                Doctor = d,
                Calendars = _db.WorkingCalendars
                    .Where(w => w.DoctorID == d.Id &&
                        w.Date >= startOfMonth &&
                        w.Date <= endOfMonth)
                    .Select(w => new
                    {
                        Calendar = w,
                        Times = _db.TimeWorkings.Where(t => t.CalendarID == w.Id).ToList(),
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

            foreach (var doctorData in partTimeDoctors)
            {
                var insufficientHours = false;

                var weeklyData = doctorData.Calendars
                    .GroupBy(c => GetWeekOfMonth(DateTime.Parse(c.Calendar.Date.Value.ToString())))
                    .Select(g => new
                    {
                        WeekNumber = g.Key,
                        FirstDate = g.Min(c => c.Calendar.Date.Value),
                        Hours = g.Sum(c => c.Times.Sum(t => (t.EndTime - t.StartTime).TotalHours))
                    })
                    .OrderBy(w => w.FirstDate)
                    .ToList();

                if (weeklyData.Last().WeekNumber < 4)
                {
                    insufficientHours = true;
                }
                else
                {
                    foreach (var weekData in weeklyData)
                    {
                        if (weekData == weeklyData.First() &&
                        weekData.FirstDate.DayOfWeek >= DayOfWeek.Wednesday)
                        {
                            continue;
                        }

                        if (weekData == weeklyData.Last() &&
                            weekData.FirstDate.AddDays(6).Month != targetMonth)
                        {
                            continue;
                        }

                        if (weekData.Hours < 20)
                        {
                            insufficientHours = true;
                            break;
                        }
                    }
                }
                if (insufficientHours)
                {
                    var user = await _userManager.FindByIdAsync(doctorData.Doctor.DoctorId);
                    if (user.IsActive)
                    {
                        result.Add(new GetDoctorResponse
                        {
                            DoctorProfile = doctorData.Doctor,
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
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting doctors with no calendar: {Message}", ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<TimeDetail>> GetTimeWorkingAsync(GetTimeWorkingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.UserID == null)
            {
                throw new Exception("Warning: user information should be include.");
            }
            if (request.Date == default)
            {
                throw new Exception("Warning: The Date is default.");
            }

            var dProfile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == request.UserID);

            if (dProfile == null)
            {
                throw new Exception("Warning: Can not identify doctor.");
            }
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == dProfile.Id && p.Date == request.Date);
            if (calendar == null)
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
        catch (Exception ex)
        {
            _logger.LogError("Error getting doctors with no calendar: {Message}", ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> AutoAddRoomForWorkingAsync(List<Guid> request, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in request)
            {
                var calendar = await _db.WorkingCalendars
                    .Where(p => p.Id == item)
                    .Select(c => new
                    {
                        Calendar = c,
                        Times = _db.TimeWorkings.Where(p => p.CalendarID == c.Id).ToList(),
                        Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == c.DoctorID)
                    })
                    .FirstOrDefaultAsync();
                if (calendar == null)
                {
                    throw new Exception("Warning: Error when find calendar");
                }
                //if (calendar.Times.Count() == 0)
                //{
                //    throw new Exception("Warning: Time was not selected.");
                //}
                if (calendar.Doctor.WorkingType == WorkingType.PartTime && calendar.Calendar.Status != WorkingStatus.Accept)
                {
                    throw new Exception("Warning: Time working that is not accept.");
                }
                if (!calendar.Times.Any())
                {
                    var shifts = new List<(TimeSpan Start, TimeSpan End)>
                    {
                        (new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0)),
                        (new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0)),
                        (new TimeSpan(18, 0, 0), new TimeSpan(22, 0, 0))
                    };
                    var room_id = Guid.Empty;
                    var assignedShifts = new List<int>();
                    for (int i = 1; i <= 2; i++)
                    {
                        for (int j = 0; j < shifts.Count(); j++){

                            if (assignedShifts.Contains(j))
                                continue;

                            var wasUse = await _db.WorkingCalendars
                            .Where(p => p.Id != calendar.Calendar.Id &&
                            p.Date == calendar.Calendar.Date &&
                            p.Status == WorkingStatus.Accept &&
                            p.RoomID != default &&
                            _db.TimeWorkings.Any(c =>
                                c.CalendarID != calendar.Calendar.Id && (
                                c.StartTime <= shifts[j].Start && shifts[j].Start < c.EndTime ||
                                c.StartTime < shifts[j].End && shifts[j].End <= c.EndTime ||
                                shifts[j].Start <= c.StartTime && c.EndTime <= shifts[j].End
                            ))).ToListAsync();
                            if (wasUse.Count() > 0)
                            {
                                var room = await _db.Rooms.ToListAsync();
                                foreach (var r in wasUse)
                                {
                                    room.RemoveAll(v => v.Id == r.RoomID);
                                }
                                if (room.Count() == 0)
                                {
                                    continue;
                                }
                                if (room_id != default && !wasUse.Any(p => p.RoomID == room_id))
                                {
                                    calendar.Calendar.RoomID = room_id;
                                    _db.TimeWorkings.Add(new TimeWorking
                                    {
                                        CalendarID = calendar.Calendar.Id,
                                        IsActive = true,
                                        StartTime = shifts[j].Start,
                                        EndTime = shifts[j].End,
                                    });
                                    assignedShifts.Add(j);
                                    break;
                                }
                                else
                                {
                                    calendar.Calendar.RoomID = room[0].Id;
                                    _db.TimeWorkings.Add(new TimeWorking
                                    {
                                        CalendarID = calendar.Calendar.Id,
                                        IsActive = true,
                                        StartTime = shifts[j].Start,
                                        EndTime = shifts[j].End,
                                    });
                                    room_id = room[0].Id;
                                    assignedShifts.Add(j);
                                    break;
                                }
                            }
                            else
                            {
                                if (room_id == default)
                                {
                                    var room = await _db.Rooms.ToListAsync();
                                    calendar.Calendar.RoomID = room[0].Id;
                                    _db.TimeWorkings.Add(new TimeWorking
                                    {
                                        CalendarID = calendar.Calendar.Id,
                                        IsActive = true,
                                        StartTime = shifts[j].Start,
                                        EndTime = shifts[j].End,
                                    });
                                    room_id = room[0].Id;
                                    assignedShifts.Add(j);
                                    break;
                                }
                                else
                                {
                                    calendar.Calendar.RoomID = room_id;
                                    _db.TimeWorkings.Add(new TimeWorking
                                    {
                                        CalendarID = calendar.Calendar.Id,
                                        IsActive = true,
                                        StartTime = shifts[j].Start,
                                        EndTime = shifts[j].End,
                                    });
                                    assignedShifts.Add(j);
                                    break;
                                }
                            }
                        }
                    }
                    if (assignedShifts.Count < 2)
                    {
                        throw new Exception($"Warning: Could not assign enough shifts at {calendar.Calendar.Date}. Only assigned {assignedShifts.Count} shifts.");
                    }
                    calendar.Calendar.Status = WorkingStatus.Accept;
                }
                else
                {
                    var room_id = Guid.Empty;
                    foreach (var item2 in calendar.Times)
                    {
                        var wasUse = await _db.WorkingCalendars
                            .Where(p => p.Id != calendar.Calendar.Id &&
                            p.Date == calendar.Calendar.Date &&
                            p.Status == WorkingStatus.Accept &&
                            p.RoomID != default &&
                            _db.TimeWorkings.Any(c =>
                                c.CalendarID != calendar.Calendar.Id && (
                                c.StartTime <= item2.StartTime && item2.StartTime < c.EndTime ||
                                c.StartTime < item2.EndTime && item2.EndTime <= c.EndTime ||
                                item2.StartTime <= c.StartTime && c.EndTime <= item2.EndTime
                            ))).ToListAsync();

                        if (wasUse == null)
                        {
                            if (room_id == Guid.Empty)
                            {
                                var room = await _db.Rooms.FirstOrDefaultAsync();
                                room_id = room.Id;
                                calendar.Calendar.RoomID = room.Id;
                            }
                            else
                            {
                                calendar.Calendar.RoomID = room_id;
                            }

                        }
                        else
                        {
                            bool isUse = wasUse.Any(p => p.Status == WorkingStatus.Accept && p.RoomID == room_id);
                            if (!isUse && room_id != default)
                            {
                                calendar.Calendar.RoomID = room_id;
                            }
                            else
                            {
                                var room = await _db.Rooms.ToListAsync();
                                foreach (var r in wasUse)
                                {
                                    room.RemoveAll(v => v.Id == r.RoomID);
                                }
                                if (room.Count() == 0)
                                {
                                    throw new Exception($"Warning: All room has been taken at {calendar.Calendar.Date} {item2.StartTime} - {item2.EndTime}");
                                }
                                calendar.Calendar.RoomID = room[0].Id;
                                room_id = calendar.Calendar.RoomID;
                            }
                        }
                    }
                }
                if (calendar.Doctor.WorkingType == WorkingType.PartTime)
                {
                    calendar.Calendar.Status = WorkingStatus.Accept;
                    foreach (var i in calendar.Times)
                    {
                        i.IsActive = true;
                    }
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await _appointmentService.DeleteRedisCode();
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex.Message, ex);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> SendNotiReminderAsync(string id, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            if (date == default)
            {
                throw new Exception("The date information should be include.");
            }
            var user = await _db.Users.FirstOrDefaultAsync(p => p.Id == id) ?? throw new Exception("User Not Found");
            var dprofile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == user.Id) ?? throw new Exception("Error when found doctor information");

            int r = await _db.WorkingCalendars.CountAsync(p => p.DoctorID == dprofile.Id && p.Date.Value.Month == date.Month && p.Status != WorkingStatus.Off);
            string message = "";
            if (r == 0)
            {
                message = $"Bạn chưa đăng ký lịch làm cho {date.Month}/{date.Year}";
            }
            else
            {
                message = $"Bạn mới đăng ký {r} ngày cho {date.Month}/{date.Year}";
            }
            await _notificationService.SendNotificationToUser(dprofile.DoctorId,
                       new Shared.Notifications.BasicNotification
                       {
                           Label = Shared.Notifications.BasicNotification.LabelType.Success,
                           Message = message,
                           Title = "Nhắc nhở đăng ký lịch làm",
                           Url = "/working-calendar",
                       }, null, cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<Stream> ExportWorkingCalendarAsync(DateOnly start, DateOnly end)
    {
        try
        {
            var query = _db.WorkingCalendars.AsQueryable();
            if (start != default)
            {
                query = query.Where(p => p.Date >= start);
            }
            if (end != default)
            {
                query = query.Where(p => p.Date <= end);
            }
            query = query.OrderBy(p => p.Date);
            var r = await query
                .Select(a => new
                {
                    Calendar = a,
                    Doctor = _db.DoctorProfiles
                        .Where(p => p.Id == a.DoctorID)
                        .Join(_db.Users, p => p.DoctorId, u => u.Id, (p, u) => u).FirstOrDefault(),
                    TypeWorking = _db.DoctorProfiles.Where(p => p.Id == a.DoctorID).Select(t => t.WorkingType.ToString()).FirstOrDefault(),
                    TypeService = _db.DoctorProfiles
                        .Where(p => p.Id == a.DoctorID)
                        .Join(_db.TypeServices, p => p.TypeServiceID, u => u.Id, (p, u) => u.TypeName).FirstOrDefault(),
                    Date = a.Date.Value.ToString("dd-MM-yyyy"),
                    Room = _db.Rooms.Where(p => p.Id == a.RoomID).Select(r => r.RoomName).FirstOrDefault(),
                    Times = _db.TimeWorkings.Where(p => p.CalendarID == a.Id).OrderBy(p => p.StartTime).Select(t => new { Time = $"{t.StartTime} - {t.EndTime}", Status = t.IsActive }).ToList(),
                }).ToListAsync();
            var groupedResults = r.GroupBy(x => new { x.Doctor }).ToList();
            var sheetData = new Dictionary<string, List<WorkingCalendarExport>>();

            foreach (var group in groupedResults)
            {
                var doctorCalendars = new List<WorkingCalendarExport>();

                foreach (var item in group)
                {
                    string GetStatus(bool isActive, DateOnly calendarDate)
                    {
                        if (!isActive) return "Absent";
                        return calendarDate <= DateOnly.FromDateTime(DateTime.Now) ? "Present" : "Waiting";
                    }
                    var form = await _db.ApplicationForms.FirstOrDefaultAsync(p => p.CalendarID == item.Calendar.Id);
                    doctorCalendars.Add(new WorkingCalendarExport
                    {
                        Doctor = $"{item.Doctor.FirstName} {item.Doctor.LastName}",
                        TypeWorking = item.TypeWorking,
                        TypeService = item.TypeService,
                        Date = item.Date,
                        Room = item.Room,
                        First_Shift = item.Times[0].Time,
                        First_Status = GetStatus(item.Times[0].Status, item.Calendar.Date.Value),
                        Last_Shift = item.Times.Count > 1 ? item.Times[1].Time : null,
                        Second_Status = item.Times.Count > 1 ? GetStatus(item.Times[1].Status, item.Calendar.Date.Value) : null,
                        Description = form != null ? form.Description : null,
                        Note = form != null ? form.Note : null,
                    });
                }

                string sheetName = $"{group.Key.Doctor.FirstName} {group.Key.Doctor.LastName}";

                sheetData.Add(sheetName, doctorCalendars);
            }


            return _excelWriter.WriteToStreamWithMultipleSheets(sheetData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }
}
