﻿using DocumentFormat.OpenXml.Office2010.Excel;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
using FSH.WebApi.Application.Dashboards;
using FSH.WebApi.Application.Notifications;
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

    public DashboardService(ApplicationDbContext db, IStringLocalizer<DashboardService> t, UserManager<ApplicationUser> userManager, IJobService jobService, ILogger<DashboardService> logger, ICacheService cacheService, INotificationService notificationService)
    {
        _db = db;
        _t = t;
        _userManager = userManager;
        _jobService = jobService;
        _logger = logger;
        _cacheService = cacheService;
        _notificationService = notificationService;
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
}
