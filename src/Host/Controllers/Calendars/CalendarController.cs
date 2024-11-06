using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.WorkingCalendars;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FSH.WebApi.Host.Controllers.Calendars;
public class CalendarController : VersionNeutralApiController
{
    private readonly IWorkingCalendarService _workingCalendarService;

    public CalendarController(IWorkingCalendarService workingCalendarService)
    {
        _workingCalendarService = workingCalendarService;
    }

    //checked
    [HttpPost("/get-schedules")]
    [OpenApiOperation("Get Working Schedule for all Doctor.", "")]
    public Task<PaginationResponse<WorkingCalendarResponse>> GetWorkingSchedulesAsync(PaginationFilter filter, CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId || string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException();
        }
        return _workingCalendarService.GetWorkingCalendars(filter, cancellationToken);
    }

    [HttpPost("/available-time")]
    [OpenApiOperation("Get Available Time Slot.", "")]
    public Task<List<AvailableTimeResponse>> GetAvailableTimeSlotAsync(GetAvailableTimeRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }
}
