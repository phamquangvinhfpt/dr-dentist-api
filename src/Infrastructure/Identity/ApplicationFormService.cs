using Ardalis.Specification.EntityFrameworkCore;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Mailing;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.Identity.ApplicationForms;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Domain.Appointments;
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Identity;

internal class ApplicationFormService : IApplicationFormService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly ICurrentUser _currentUserService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJobService _jobService;
    private readonly ILogger<ApplicationFormService> _logger;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;
    private readonly IEmailTemplateService _templateService;
    private readonly IMailService _mailService;
    private readonly IAppointmentService _appointmentService;

    public ApplicationFormService(IAppointmentService appointmentService, ApplicationDbContext db, IStringLocalizer<ApplicationFormService> t, ICurrentUser currentUserService, UserManager<ApplicationUser> userManager, IJobService jobService, ILogger<ApplicationFormService> logger, ICacheService cacheService, INotificationService notificationService, IEmailTemplateService templateService, IMailService mailService)
    {
        _db = db;
        _t = t;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
        _cacheService = cacheService;
        _notificationService = notificationService;
        _templateService = templateService;
        _mailService = mailService;
        _appointmentService = appointmentService;
    }

    public async Task<string> AddFormAsync(AddFormRequest form, CancellationToken cancellationToken)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await _userManager.FindByIdAsync(form.UserID);
            if (user == null) {
                throw new Exception("Warning: User Not Found");
            }
            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.Id == form.CalendarID);
            if (calendar == null)
            {
                throw new Exception("Warning: Calendar Not Found");
            }
            else if (calendar.Status != Domain.Identity.WorkingStatus.Accept) {
                throw new Exception($"Warning: Your Calendar was {calendar.Status.ToString()}");
            }
            if(form.Description == null)
            {
                throw new Exception("Warning: Lack off description.");
            }
            var existingForm = await _db.ApplicationForms.Where(p => p.CalendarID == form.CalendarID).FirstOrDefaultAsync();
            if (existingForm != null){
                if (form.TimeID == existingForm.TimeID) {
                    throw new Exception($"Warning: Your application form was created");
                }
                if(form.TimeID != default)
                {
                    var time = await _db.TimeWorkings.FirstOrDefaultAsync(p => p.Id == form.TimeID);
                    if (time == null)
                    {
                        throw new Exception("Warning: Time Not Found");
                    }
                    else if (!time.IsActive)
                    {
                        throw new Exception("Warning: Your time that you selected, was not active");
                    }
                }
                existingForm.TimeID = default;
                existingForm.Description = string.Concat(existingForm.Description, ", ", form.Description);
            }
            else
            {
                var app = new ApplicationForm
                {
                    UserID = form.UserID,
                    CalendarID = form.CalendarID,
                    Description = form.Description,
                    Status = FormStatus.Waiting,
                };
                if (form.TimeID != default)
                {
                    var time = await _db.TimeWorkings.FirstOrDefaultAsync(p => p.Id == form.TimeID);
                    if (time == null)
                    {
                        throw new Exception("Warning: Time Not Found");
                    }
                    else if (!time.IsActive)
                    {
                        throw new Exception("Warning: Your time that you selected, was not active");
                    }
                    app.TimeID = time.Id;
                }
                _db.ApplicationForms.Add(app);
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return "Success";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<FormDetailResponse>> GetFormDetails(PaginationFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            string currentUser = _currentUserService.GetRole();
            var result = new List<FormDetailResponse>();
            var spec = new EntitiesByPaginationFilterSpec<ApplicationForm>(filter);


            var query = _db.ApplicationForms
                .AsNoTracking();

            if(currentUser == FSHRoles.Dentist)
            {
                query = query.Where(p => p.UserID == _currentUserService.GetUserId().ToString());
            }


            var forms = await query
                .OrderByDescending(p => p.CreatedOn)
                .WithSpecification(spec)
                .ToListAsync();
            int count = await _db.ApplicationForms.CountAsync();
            foreach (var form in forms) {
                var user = await _userManager.FindByIdAsync(form.UserID);
                var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.Id == form.CalendarID);
                var times = new List<TimeDetail>();
                if (form.TimeID != default)
                {
                    var time = await _db.TimeWorkings.FirstOrDefaultAsync(p => p.Id == form.TimeID);
                    times.Add(new TimeDetail
                    {
                        TimeID = form.TimeID,
                        IsActive = time.IsActive,
                        EndTime = time.EndTime,
                        StartTime = time.StartTime,
                    });
                }
                else {
                    var ts = await _db.TimeWorkings.Where(p => p.CalendarID == form.CalendarID).ToListAsync();
                    foreach (var time in ts) {
                        times.Add(new TimeDetail
                        {
                            TimeID = form.TimeID,
                            IsActive = time.IsActive,
                            EndTime = time.EndTime,
                            StartTime = time.StartTime,
                        });
                    }
                }
                result.Add(new FormDetailResponse
                {
                    FormID = form.Id,
                    CalendarID = form.CalendarID,
                    Description = form.Description,
                    Status = form.Status,
                    Note = form.Note,
                    UserID = form.UserID,
                    Name = $"{user.FirstName} {user.LastName}",
                    WorkingDate = calendar.Date.Value,
                    WorkingTimes = times
                });
            }
            return new PaginationResponse<FormDetailResponse>(result, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<string> ToggleFormAsync(ToggleFormRequest form, CancellationToken cancellationToken)
    {
        try
        {
            var existingForm = await _db.ApplicationForms.FirstOrDefaultAsync(p => p.Id == form.FormId);
            if (existingForm == null) {
                throw new Exception("Warning: Form Not Found");
            }
            if (existingForm.Status != FormStatus.Waiting) {
                throw new Exception($"Warning: Form was {existingForm.Status.ToString()}");
            }
            if (form.Note == null)
            {
                throw new Exception("Warning: Form Description Not Found");
            }
            existingForm.Status = form.Status;
            existingForm.Note = form.Note;

            var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.Id == existingForm.CalendarID);

            if (form.Status == FormStatus.Accepted)
            {
                if (existingForm.TimeID != default)
                {
                    var time = await _db.TimeWorkings.FirstOrDefaultAsync(p => p.Id == existingForm.TimeID);
                    time.IsActive = false;
                    var isOffAllDay = await _db.TimeWorkings.CountAsync(p => p.CalendarID == calendar.Id && p.IsActive);
                    if (isOffAllDay == 1)
                    {
                        calendar.Status = WorkingStatus.Off;
                    }
                }
                else
                {
                    var ts = await _db.TimeWorkings.Where(p => p.CalendarID == existingForm.CalendarID).ToListAsync();
                    foreach (var time in ts)
                    {
                        time.IsActive = false;
                    }
                    calendar.Status = WorkingStatus.Off;
                }

            }
            await _db.SaveChangesAsync(cancellationToken);
            await SendAppointmentActionNotification(existingForm.UserID, calendar.Date.Value, form.Status, cancellationToken);
            await _appointmentService.DeleteRedisCode();
            return "Success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task SendAppointmentActionNotification(string DoctorID, DateOnly Date, FormStatus type, CancellationToken cancellationToken)
    {
        try
        {
            var doctor = await _userManager.FindByIdAsync(DoctorID);
            switch (type)
            {
                case FormStatus.Failed:
                    await _notificationService.SendNotificationToUser(doctor.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Information,
                            Message = $"Đơn xin nghỉ ngày {Date} đã không thông qua",
                            Title = "Thông báo",
                            Url = null,
                        }, null, cancellationToken);
                    break;
                case FormStatus.Accepted:
                    await _notificationService.SendNotificationToUser(doctor.Id,
                        new Shared.Notifications.BasicNotification
                        {
                            Label = Shared.Notifications.BasicNotification.LabelType.Information,
                            Message = $"Đơn xin nghỉ ngày {Date} đã được thông qua",
                            Title = "Thông báo",
                            Url = null,
                        }, null, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }
}
