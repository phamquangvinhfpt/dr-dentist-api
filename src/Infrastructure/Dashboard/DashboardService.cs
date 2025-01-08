using Ardalis.Specification.EntityFrameworkCore;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Models;
using FSH.WebApi.Application.Common.Specification;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Application.Dashboards;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Appointments;
using FSH.WebApi.Infrastructure.Auth.Permissions;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NSwag.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Dashboard;
internal class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<DashboardService> _t;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJobService _jobService;
    private readonly ILogger<DashboardService> _logger;
    private readonly ICacheService _cacheService;
    private readonly INotificationService _notificationService;
    private readonly IAppointmentCalendarService _appointmentCalendarService;

    public DashboardService(ApplicationDbContext db, IStringLocalizer<DashboardService> t, UserManager<ApplicationUser> userManager, IJobService jobService, ILogger<DashboardService> logger, ICacheService cacheService, INotificationService notificationService, IAppointmentCalendarService appointmentCalendarService)
    {
        _db = db;
        _t = t;
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
        _cacheService = cacheService;
        _notificationService = notificationService;
        _appointmentCalendarService = appointmentCalendarService;
    }

    public async Task<int> AppointmentDoneAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Appointments.CountAsync(p => p.Status == Domain.Appointments.AppointmentStatus.Done);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<BookingAnalytic>> BookingAnalytics(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            var chartQuery = _db.Appointments.Where(p => p.Status != Domain.Appointments.AppointmentStatus.Pending);

            if (startDate != default)
            {
                chartQuery = chartQuery.Where(p => startDate <= p.AppointmentDate);
            }
            if (endDate != default)
            {
                chartQuery = chartQuery.Where(p => p.AppointmentDate <= endDate);
            }
            var chart = await chartQuery
            .GroupBy(p => p.AppointmentDate)
            .Select(n => new BookingAnalytic
            {
                Date = n.Key,
                CancelAnalytic = n.Count(p => p.Status == Domain.Appointments.AppointmentStatus.Cancelled),
                FailAnalytic = n.Count(p => p.Status == Domain.Appointments.AppointmentStatus.Failed),
                SuccessAnalytic = n.Count(p => p.Status == Domain.Appointments.AppointmentStatus.Come ||
                                             p.Status == Domain.Appointments.AppointmentStatus.Done)
            })
            .OrderBy(p => p.Date)
            .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<DoctorAnalytic>> DoctorAnalytic(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            var chartQuery = _db.Appointments.Where(p => p.Status == Domain.Appointments.AppointmentStatus.Done);

            if (startDate != default)
            {
                chartQuery = chartQuery.Where(p => startDate <= p.AppointmentDate);
            }
            if (endDate != default)
            {
                chartQuery = chartQuery.Where(p => p.AppointmentDate <= endDate);
            }
            var chart = await chartQuery
                    .GroupBy(p => p.DentistId)
                    .Select(n => new 
                    {
                        DoctorID = n.Key,
                        TotalRating = _db.Feedbacks
                                .Where(f => f.DoctorProfileId == n.Key)
                                .GroupBy(f => f.ServiceId)
                                .Select(group => new
                                {
                                    AverageRating = group.Average(f => f.Rating)
                                })
                                .FirstOrDefault().AverageRating,
                    }).OrderByDescending(p => p.TotalRating)
                    .ToListAsync(cancellationToken);
            List<DoctorAnalytic> result = new List<DoctorAnalytic>();
            foreach (var item in chart) {
                var d = await _db.DoctorProfiles.Where(p => p.Id == item.DoctorID)
                    .Select(n => new
                    {
                        Doctor = _db.Users.FirstOrDefault(p => p.Id == n.DoctorId),
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                result.Add(new Application.Dashboards.DoctorAnalytic
                {
                    DoctorId = d.Doctor.Id,
                    DoctorName = $"{d.Doctor.FirstName} {d.Doctor.LastName}",
                    TotalRating = Math.Round(item.TotalRating, 0),
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<AppointmentResponse>> GetAppointmentAsync(DateOnly date, PaginationFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<AppointmentResponse>();
            var spec = new EntitiesByPaginationFilterSpec<AppointmentResponse>(filter);
            var appointmentsQuery = _db.Appointments
                .AsQueryable().Where(p => p.DentistId != default && p.Status == AppointmentStatus.Confirmed);

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.AppointmentDate == date);
            }
            //appointmentsQuery = appointmentsQuery.Where(p => !_db.WorkingCalendars.Any(w => w.DoctorID == p.DentistId &&
            //    w.Date == p.AppointmentDate &&
            //    w.Status == Domain.Identity.WorkingStatus.Accept));

            //int count = await appointmentsQuery.CountAsync(cancellationToken);

            appointmentsQuery = appointmentsQuery.OrderBy(p => p.StartTime);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(s => s.Id == appointment.ServiceId),
                    Payment = _db.Payments.IgnoreQueryFilters().FirstOrDefault(p => p.AppointmentId == appointment.Id),
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DentistId),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                if(!_appointmentCalendarService.CheckAvailableTimeSlotForDash(a.Appointment.AppointmentDate, a.Appointment.StartTime, a.Appointment.StartTime.Add(a.Appointment.Duration), a.Appointment.DentistId).Result)
                {
                    var patient = _db.Users.FirstOrDefaultAsync(p => p.Id == a.Patient.UserId).Result;
                    var dUser = await _userManager.FindByIdAsync(a.Doctor.DoctorId);
                    result.Add(new AppointmentResponse
                    {
                        PatientUserID = patient.Id,
                        AppointmentId = a.Appointment.Id,
                        PatientId = a.Appointment.PatientId,
                        ServiceId = a.Appointment.ServiceId,
                        AppointmentDate = a.Appointment.AppointmentDate,
                        StartTime = a.Appointment.StartTime,
                        Duration = a.Appointment.Duration,
                        Status = a.Appointment.Status,
                        Notes = a.Appointment.Notes,
                        PatientPhone = patient.PhoneNumber != null ? patient.PhoneNumber : null,
                        PatientCode = a.Patient?.PatientCode,
                        PatientName = $"{patient.FirstName} {patient.LastName}",
                        ServiceName = a.Service?.ServiceName,
                        ServicePrice = a.Service?.TotalPrice ?? 0,
                        PaymentStatus = a.Payment is not null ? a.Payment.Status : Domain.Payments.PaymentStatus.Waiting,
                        PatientAvatar = patient.ImageUrl != null ? patient.ImageUrl : null,
                        DentistId = a.Doctor.Id,
                        DentistName = $"{dUser.FirstName} {dUser.LastName}",
                        Type = AppointmentType.Appointment
                    });
                }
            }
            int count = result.Count();
            var r = result.AsQueryable().WithSpecification(spec).ToList();
            return new PaginationResponse<AppointmentResponse>(r, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PercentChart> GetBookingPercent(DateOnly start, CancellationToken cancellationToken)
    {
        try
        {
            var weekStart = start.AddDays(-((int)start.DayOfWeek -1));
            var weekEnd = weekStart.AddDays(6);

            var previousWeekStart = weekStart.AddDays(-7);
            var previousWeekEnd = weekEnd.AddDays(-7);

            var currentWeekBookings = await _db.Appointments
                .Where(a => a.AppointmentDate >= weekStart && a.AppointmentDate <= weekEnd)
                .CountAsync(cancellationToken);

            var previousWeekBookings = await _db.Appointments
                .Where(a => a.AppointmentDate >= previousWeekStart && a.AppointmentDate <= previousWeekEnd)
                .CountAsync(cancellationToken);

            double percentChange = (currentWeekBookings - previousWeekBookings) * 100 / previousWeekBookings;

            return new PercentChart
            {
                Value = currentWeekBookings,
                Percent = percentChange
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetFollowUpAsync(DateOnly date, PaginationFilter filter, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<GetWorkingDetailResponse>();
            var spec = new EntitiesByPaginationFilterSpec<GetWorkingDetailResponse>(filter);
            var appointmentsQuery = _db.AppointmentCalendars
                .AsNoTracking()
                .Where(p => p.Type == AppointmentType.FollowUp && p.Status == Domain.Identity.CalendarStatus.Booked);

            if (date != default)
            {
                appointmentsQuery = appointmentsQuery.Where(w => w.Date == date);
            }
            //appointmentsQuery = appointmentsQuery.Where(p => !_db.WorkingCalendars.Any(w => w.DoctorID == p.DentistId &&
            //    w.Date == p.AppointmentDate &&
            //    w.Status == Domain.Identity.WorkingStatus.Accept));

            //int count = await appointmentsQuery.CountAsync(cancellationToken);

            appointmentsQuery = appointmentsQuery.OrderBy(p => p.StartTime);

            var appointments = await appointmentsQuery
                .Select(appointment => new
                {
                    Appointment = appointment,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(d => d.Id == appointment.DoctorId),
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == appointment.PatientId),
                    TreatmentPlan = _db.TreatmentPlanProcedures.FirstOrDefault(s => s.Id == appointment.PlanID),
                })
                .ToListAsync(cancellationToken);

            foreach (var a in appointments)
            {
                if (!_appointmentCalendarService.CheckAvailableTimeSlotForDash(a.Appointment.Date.Value, a.Appointment.StartTime.Value, a.Appointment.EndTime.Value, a.Appointment.DoctorId.Value).Result)
                {
                    var doctor = await _userManager.FindByIdAsync(a.Doctor.DoctorId);
                    var patient = await _userManager.FindByIdAsync(a.Patient.UserId);
                    var sp = await _db.ServiceProcedures.Where(p => p.Id == a.TreatmentPlan.ServiceProcedureId)
                        .Select(s => new
                        {
                            Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(p => p.Id == s.ServiceId),
                            Procedure = _db.Procedures.IgnoreQueryFilters().FirstOrDefault(p => p.Id == s.ProcedureId),
                            Step = s.StepOrder
                        }).FirstOrDefaultAsync();
                    var calendar = await _db.WorkingCalendars.FirstOrDefaultAsync(p => p.DoctorID == a.Doctor.Id && p.Date == a.Appointment.Date && p.Status == WorkingStatus.Accept);
                    var r = new GetWorkingDetailResponse
                    {
                        TreatmentID = a.TreatmentPlan.Id,
                        AppointmentId = a.Appointment.AppointmentId.Value,
                        AppointmentType = a.Appointment.Type,
                        CalendarID = a.Appointment.Id,
                        Date = a.Appointment.Date.Value,
                        DoctorName = $"{doctor.FirstName} {doctor.LastName}",
                        DoctorProfileID = a.Appointment.DoctorId.Value,
                        EndTime = a.Appointment.EndTime.Value,
                        PatientPhone = patient.PhoneNumber != null ? patient.PhoneNumber : null,
                        Note = a.Appointment.Note,
                        PatientCode = a.Patient.PatientCode,
                        PatientName = $"{patient.FirstName} {patient.LastName}",
                        PatientProfileID = a.Patient.Id,
                        ProcedureID = sp.Procedure.Id,
                        ProcedureName = sp.Procedure.Name,
                        ServiceID = sp.Service.Id,
                        ServiceName = sp.Service.ServiceName,
                        StartTime = a.Appointment.StartTime.Value,
                        Status = a.Appointment.Status,
                        Step = sp.Step,
                        PatientAvatar = patient.ImageUrl != null ? patient.ImageUrl : null,
                        DoctorUserID = doctor.Id
                    };
                    if (calendar != null)
                    {
                        if (calendar.RoomID != default)
                        {
                            var room = await _db.Rooms.FirstOrDefaultAsync(p => p.Id == calendar.RoomID);
                            r.RoomID = room.Id;
                            r.RoomName = room.RoomName;
                        }
                    }
                    result.Add(r);
                }
            }
            int count = result.Count();
            var re = result.AsQueryable().WithSpecification(spec).ToList();
            return new PaginationResponse<GetWorkingDetailResponse>(re, count, filter.PageNumber, filter.PageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<AnalyticChart>> GetNewDepositBooking(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            var chartQuery = _db.Payments.Where(p => p.Status == Domain.Payments.PaymentStatus.Incomplete);

            if (startDate != default)
            {
                chartQuery = chartQuery.Where(p => startDate <= p.DepositDate);
            }
            if (endDate != default)
            {
                chartQuery = chartQuery.Where(p => p.DepositDate <= endDate);
            }
            var chart = await chartQuery.OrderBy(p => p.DepositDate)
                    .GroupBy(p => p.DepositDate)
                    .Select(n => new AnalyticChart
                    {
                        Date = n.Key.Value,
                        Total = n.Sum(p => p.Amount).Value,
                    })
                    .OrderBy(p => p.Date)
                    .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<AnalyticChart>> GetRevenueChartForAdmin(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            var chartQuery = _db.Payments.Where(p => p.Status == Domain.Payments.PaymentStatus.Completed);

            if(startDate != default)
            {
                chartQuery = chartQuery.Where(p => startDate <= p.FinalPaymentDate);
            }
            if (endDate != default)
            {
                chartQuery = chartQuery.Where(p => p.FinalPaymentDate <= endDate);
            }
            var chart = await chartQuery.OrderBy(p => p.FinalPaymentDate)
                    .GroupBy(p => p.FinalPaymentDate)
                    .Select(n => new AnalyticChart
                    {
                        Date = n.Key.Value,
                        Total = n.Sum(p => p.Amount).Value,
                    })
                    .OrderBy(p => p.Date)
                    .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<PercentChart> GetRevenuePercent(DateOnly start, CancellationToken cancellationToken)
    {
        try
        {
            var weekStart = start.AddDays(-((int)start.DayOfWeek - 1));
            var weekEnd = weekStart.AddDays(6);

            var previousWeekStart = weekStart.AddDays(-7);
            var previousWeekEnd = weekEnd.AddDays(-7);

            var currentWeek = await _db.Payments
                .Where(a => a.Status == Domain.Payments.PaymentStatus.Completed && (a.FinalPaymentDate >= weekStart && a.FinalPaymentDate <= weekEnd))
                .ToListAsync(cancellationToken);

            var previousWeek = await _db.Payments
                .Where(a => (a.FinalPaymentDate >= previousWeekStart && a.FinalPaymentDate <= previousWeekEnd))
                .ToListAsync(cancellationToken);

            var percentChange = (currentWeek.Sum(p => p.Amount) - previousWeek.Sum(p => p.Amount)) * 100 / previousWeek.Sum(p => p.Amount);

            return new PercentChart
            {
                Value = ((int)currentWeek.Sum(p => p.Amount).Value),
                Percent = Math.Round(percentChange.Value, 0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<AnalyticChart>> MemberShipGrowth(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            var chartQuery = _db.PatientProfiles.AsNoTracking();

            if (startDate != default)
            {
                chartQuery = chartQuery.Where(p => startDate <= DateOnly.FromDateTime(p.CreatedOn));
            }
            if (endDate != default)
            {
                chartQuery = chartQuery.Where(p => DateOnly.FromDateTime(p.CreatedOn) <= endDate);
            }
            var chart = await chartQuery.OrderBy(p => p.CreatedOn)
                    .GroupBy(p => p.CreatedOn)
                    .Select(n => new AnalyticChart
                    {
                        Date = DateOnly.FromDateTime(n.Key),
                        Total = n.Count(),
                    })
                    .GroupBy(p => p.Date)
                    .Select(n => new AnalyticChart
                    {
                        Date = n.Key,
                        Total = n.Count(),
                    })
                    .OrderBy(p => p.Date)
                    .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<int> NewContactsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            return await _db.ContactInfor.CountAsync(p => p.Status == Domain.CustomerServices.ContactStatus.Pending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<FeedbackServiceDetail>> PatientFeedbacksAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<FeedbackServiceDetail>();

            var query = await _db.Feedbacks
                .Where(p => p.Rating > 3)
                .OrderBy(p => p.CreatedOn)
                .Take(3)
                .Select(a => new
                {
                    Feedback = a,
                    Doctor = _db.DoctorProfiles.FirstOrDefault(p => p.Id == a.DoctorProfileId),
                    Patient = _db.PatientProfiles.FirstOrDefault(p => p.Id == a.PatientProfileId),
                    Service = _db.Services.IgnoreQueryFilters().FirstOrDefault(p => p.Id == a.ServiceId),
                })
                .ToListAsync();
            foreach(var d in query)
            {
                var dPatient = await _db.Users.FirstOrDefaultAsync(p => p.Id == d.Patient.UserId);
                var dDoctor = await _db.Users.FirstOrDefaultAsync(p => p.Id == d.Doctor.DoctorId);
                result.Add(new FeedbackServiceDetail
                {
                    FeedbackId = d.Feedback.Id,
                    PatientID = dPatient.Id,
                    CanFeedback = false,
                    CreateDate = d.Feedback.CreatedOn,
                    DoctorID = dDoctor.Id,
                    DoctorName = $"{dDoctor.FirstName} {dDoctor.LastName}",
                    Message = d.Feedback.Message,
                    PatientName = $"{dPatient.FirstName} {dPatient.LastName}",
                    Ratings = d.Feedback.Rating,
                    PatientAvatar = dPatient.ImageUrl,
                    DoctorAvatar = dDoctor.ImageUrl,
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<int> RegularDoctorAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _db.DoctorProfiles.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<int> SatisfiedPatientAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Feedbacks.CountAsync(p => p.Rating > 3);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<ServiceAnalytic>> ServiceAnalytic(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        try
        {
            var chartQuery = _db.Appointments.Where(p => p.Status == Domain.Appointments.AppointmentStatus.Done || p.Status == Domain.Appointments.AppointmentStatus.Come);

            if (startDate != default)
            {
                chartQuery = chartQuery.Where(p => startDate <= p.AppointmentDate);
            }
            if (endDate != default)
            {
                chartQuery = chartQuery.Where(p => p.AppointmentDate <= endDate);
            }
            var chart = await chartQuery
                    .GroupBy(p => p.ServiceId)
                    .Select(n => new ServiceAnalytic
                    {
                        ServiceId = n.Key,
                        ServiceName = _db.Services.FirstOrDefault(p => p.Id == n.Key).ServiceName,
                        TotalRating = Math.Round(_db.Feedbacks
                                .Where(f => f.ServiceId == n.Key)
                                .GroupBy(f => f.ServiceId)
                                .Select(group => new
                                {
                                    AverageRating = group.Average(f => f.Rating)
                                })
                                .FirstOrDefault().AverageRating, 0),
                        TotalRevenue = _db.Payments
                                .Where(p =>
                                    p.ServiceId == n.Key &&
                                    p.AppointmentId.HasValue &&
                                    chartQuery.Any(a => a.Id == p.AppointmentId.Value)
                                )
                                .Sum(p => p.Amount ?? 0)
                    }).OrderByDescending(p => p.TotalRevenue)
                    .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<int> TotalAppointmentsAsync(DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Appointments.CountAsync(p => p.DentistId != default && p.AppointmentDate == date && p.Status == Domain.Appointments.AppointmentStatus.Confirmed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<int> TotalFollowUpAsync(DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            return await _db.AppointmentCalendars.CountAsync(p => p.DoctorId != default && p.Date == date && p.Type == AppointmentType.FollowUp && p.Status == Domain.Identity.CalendarStatus.Booked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<int> TotalServiceAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Services.CountAsync(p => p.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }

    public async Task<int> TotalUnAssignAsync(DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            return await _db.Appointments.CountAsync(p => p.DentistId == default && p.AppointmentDate == date && p.Status == Domain.Appointments.AppointmentStatus.Confirmed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw new Exception(ex.Message);
        }
    }
}
