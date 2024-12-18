﻿using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Dashboards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.Dashboards;
public class DashboardController : VersionedApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }
    //checked
    [HttpGet("chart/revenue")]
    [OpenApiOperation("Get Revenue follow by Date", "")]
    public Task<List<AnalyticChart>> GetRevenue([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        return _dashboardService.GetRevenueChartForAdmin(start, end, cancellationToken);
    }

    //checked
    [HttpGet("chart/deposit")]
    [OpenApiOperation("Get Deposit follow by Date", "")]
    public Task<List<AnalyticChart>> GetDepositChart([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        return _dashboardService.GetNewDepositBooking(start, end, cancellationToken);
    }

    //checked
    [HttpGet("chart/member-growth")]
    [OpenApiOperation("Get membership growth", "")]
    public Task<List<AnalyticChart>> GetMemberShipGrowth([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        return _dashboardService.MemberShipGrowth(start, end, cancellationToken);
    }

    //checked
    [HttpGet("revenue/service")]
    [OpenApiOperation("Analytis Revenue of services follow by date", "")]
    public Task<List<ServiceAnalytic>> ServiceAnalytic([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        return _dashboardService.ServiceAnalytic(start, end, cancellationToken);
    }

    //checked
    [HttpGet("rate/doctor")]
    [OpenApiOperation("Analytis Rating of doctor follow by date", "")]
    public Task<List<DoctorAnalytic>> DoctorAnalytic([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        return _dashboardService.DoctorAnalytic(start, end, cancellationToken);
    }

    //checked
    [HttpGet("analytic/booking")]
    [OpenApiOperation("Analytis booking status", "")]
    public Task<List<BookingAnalytic>> BookingAnalytics([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        return _dashboardService.BookingAnalytics(start, end, cancellationToken);
    }
}
