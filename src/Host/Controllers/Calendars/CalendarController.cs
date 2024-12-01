using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FSH.WebApi.Host.Controllers.Calendars;
public class CalendarController : VersionNeutralApiController
{
    private readonly IAppointmentCalendarService _workingCalendarService;

    public CalendarController(IAppointmentCalendarService workingCalendarService)
    {
        _workingCalendarService = workingCalendarService;
    }

    //checked
    [HttpPost("get-schedules")]
    [OpenApiOperation("Get Working Schedule for all Doctor.", "")]
    public Task<PaginationResponse<AppointmentCalendarResponse>> GetWorkingSchedulesAsync([FromQuery] DateOnly date, PaginationFilter filter, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }
        return _workingCalendarService.GetWorkingCalendars(filter, date, cancellationToken);
    }

    [HttpPost("available-time")]
    [OpenApiOperation("Get Available Time Slot.", "")]
    public Task<List<AvailableTimeResponse>> GetAvailableTimeSlotAsync(GetAvailableTimeRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpPost("detail/{id}")]
    [OpenApiOperation("Get Working Calendar Detail.", "")]
    public async Task<GetWorkingDetailResponse> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetCalendarDetail(id, cancellationToken);
    }
}
