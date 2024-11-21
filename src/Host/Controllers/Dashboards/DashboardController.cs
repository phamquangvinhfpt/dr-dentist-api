using FSH.WebApi.Application.Appointments;
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
    [HttpGet("chart/revenue/service")]
    [OpenApiOperation("Analytis Revenue of services follow by date", "")]
    public Task<List<ServiceAnalytic>> ServiceAnalytic([FromQuery] DateOnly start, [FromQuery] DateOnly end, CancellationToken cancellationToken)
    {
        return _dashboardService.ServiceAnalytic(start, end, cancellationToken);
    }
}
