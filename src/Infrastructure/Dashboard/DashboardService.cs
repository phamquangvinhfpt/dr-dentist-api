﻿using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Dashboards;
using FSH.WebApi.Application.Identity.WorkingCalendars;
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
                    .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
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
                    .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex) {
            _logger.LogError(ex.Message);
            throw;
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
                    .ToListAsync(cancellationToken);
            return chart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }
}