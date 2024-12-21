using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
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
    //checked
    [HttpGet("patient/satisfied")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Get Satified patient", "")]
    public Task<int> SatisfiedPatientAsync(CancellationToken cancellationToken)
    {
        return _dashboardService.SatisfiedPatientAsync(cancellationToken);
    }
    //checked
    [HttpGet("doctor/regular")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Get Regular doctor", "")]
    public Task<int> RegularDoctorAsync(CancellationToken cancellationToken)
    {
        return _dashboardService.RegularDoctorAsync(cancellationToken);
    }
    //checked
    [HttpGet("service/total")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Get Total Service", "")]
    public Task<int> TotalServiceAsync(CancellationToken cancellationToken)
    {
        return _dashboardService.TotalServiceAsync(cancellationToken);
    }

    //checked
    [HttpGet("appointment/done")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Get Amount of Appointment Done", "")]
    public Task<int> AppointmentDoneAsync(CancellationToken cancellationToken)
    {
        return _dashboardService.AppointmentDoneAsync(cancellationToken);
    }
    //checked
    [HttpGet("patients/feedbacks")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Get some feedback", "")]
    public Task<List<FeedbackServiceDetail>> PatientFeedbacksAsync(CancellationToken cancellationToken)
    {
        return _dashboardService.PatientFeedbacksAsync(cancellationToken);
    }
}
