using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Domain.Examination;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FSH.WebApi.Host.Controllers.Calendars;
public class WorkingCalendarController : VersionNeutralApiController
{
    private readonly IWorkingCalendarService _workingCalendarService;

    public WorkingCalendarController(IWorkingCalendarService workingCalendarService)
    {
        _workingCalendarService = workingCalendarService;
    }

    [HttpPost("create-parttime")]
    [OpenApiOperation("Create Working Calendar for Part-time Doctor.", "")]
    public async Task<IActionResult> CreateParttimeCalendarAsync(
        [FromBody] List<CreateOrUpdateWorkingCalendar> request,
        [FromQuery] string doctorId,
        CancellationToken cancellationToken)
    {
        var result = await _workingCalendarService.CreateWorkingCalendarForParttime(request, doctorId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("working-calendar")]
    [OpenApiOperation("Get Working Calendar Accept with Pagination.", "")]
    public async Task<PaginationResponse<WorkingCalendarResponse>> GetWorkingCalendarAsync(
        PaginationFilter filter,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetWorkingCalendarPagination(filter, startDate, endDate, cancellationToken);
    }

    [HttpPost("part-time/non-accept")]
    [OpenApiOperation("Get Working Calendar of Doctor Part time non accept with Pagination.", "")]
    public async Task<PaginationResponse<WorkingCalendarResponse>> GetPartTimeWorkingCalendarsAsync(
        PaginationFilter filter,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetPartTimeNonAcceptWorkingCalendarsAsync(filter, startDate, endDate, cancellationToken);
    }
    [HttpPost("part-time/Off")]
    [OpenApiOperation("Get Working Calendar of Doctor Part time Off with Pagination.", "")]
    public async Task<PaginationResponse<WorkingCalendarResponse>> GetPartTimeOffWorkingCalendarsAsync(
        PaginationFilter filter,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetPartTimeOffWorkingCalendarsAsync(filter, startDate, endDate, cancellationToken);
    }
    [HttpPost("full-time/non-accept")]
    [OpenApiOperation("Get Working Calendar of Doctor Full time non accept with Pagination.", "")]
    public async Task<PaginationResponse<WorkingCalendarResponse>> GetFullTimeWorkingCalendarsAsync(
        PaginationFilter filter,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetFullTimeNonAcceptWorkingCalendarsAsync(filter, startDate, endDate, cancellationToken);
    }
    [HttpPost("full-time/off")]
    [OpenApiOperation("Get Working Calendar of Doctor Full time off with Pagination.", "")]
    public async Task<PaginationResponse<WorkingCalendarResponse>> GetFullTimeOffWorkingCalendarsAsync(
        PaginationFilter filter,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetFullTimeOffWorkingCalendarsAsync(filter, startDate, endDate, cancellationToken);
    }

    //[HttpPost("full-time/add-time")]
    //[OpenApiOperation("Staff Add time working for Doctor Full time.", "")]
    //public async Task<string> AddTimeForFullTimeWorkingCalendarsAsync(
    //    [FromBody] List<CreateOrUpdateWorkingCalendar> request,
    //    [FromQuery] string doctorId,
    //    CancellationToken cancellationToken)
    //{
    //    var result = await _workingCalendarService.CreateWorkingCalendarForParttime(request, doctorId, cancellationToken);
    //    return result;
    //}

    [HttpPut("update")]
    [OpenApiOperation("Update Working Calendar.", "")]
    public async Task<IActionResult> UpdateWorkingCalendarAsync(
        List<CreateOrUpdateWorkingCalendar> request,
        [FromQuery] string doctorId,
        CancellationToken cancellationToken)
    {
        var result = await _workingCalendarService.UpdateWorkingCalendar(request, doctorId, cancellationToken);
        return Ok(result);
    }

    //[HttpGet("check-available")]
    //[OpenApiOperation("Check Available Time Working for Doctor.", "")]
    //public async Task<IActionResult> CheckAvailableTimeWorkingAsync(
    //    [FromQuery] string doctorId,
    //    [FromQuery] DateOnly date,
    //    [FromQuery] TimeSpan time)
    //{
    //    var result = await _workingCalendarService.CheckAvailableTimeWorking(doctorId, date, time);
    //    return Ok(result);
    //}

    [HttpPost("register-fulltime")]
    [OpenApiOperation("Register Full-time Working Date for Doctor.", "")]
    public async Task<IActionResult> RegisterFullTimeWorkingAsync(
        [FromQuery] string doctorId,
        [FromQuery] DateTime date,
        CancellationToken cancellationToken)
    {
        var result = await _workingCalendarService.FullTimeRegistDateWorking(doctorId, date, cancellationToken);
        return Ok(result);
    }

    [HttpPost("add-room")]
    [OpenApiOperation("Add Room to Working Calendar.", "")]
    public async Task<IActionResult> AddRoomToWorkingAsync(
        List<AddRoomToWorkingRequest> request,
        CancellationToken cancellationToken)
    {
        foreach (var item in request) {
            string result = await _workingCalendarService.AddRoomForWorkingAsync(item, cancellationToken);
        }
        return Ok("Success");
    }

    [HttpPost("create-room")]
    [OpenApiOperation("Create Room.", "")]
    public async Task<IActionResult> CreateRoomAsync(
        AddRoomRequest request,
        CancellationToken cancellationToken)
    {
        string result = await _workingCalendarService.CreateRoomsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("room/get-all")]
    [OpenApiOperation("Get Room with Pagination.", "")]
    public async Task<PaginationResponse<Room>> GetRoomsAsync(
        PaginationFilter filter,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetRoomsWithPagination(filter, cancellationToken);
    }

    [HttpPost("non-accept/get-all")]
    [OpenApiOperation("Get All Non Accept with Pagination.", "")]
    public async Task<PaginationResponse<WorkingCalendarResponse>> GetAllNonAcceptAsync(
        PaginationFilter filter,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetAllNonAcceptWithPagination(filter, startDate, endDate, cancellationToken);
    }
    //[HttpPost("part-time/confirm")]
    //[OpenApiOperation("Confirm time working for part time.", "")]
    //public async Task<PaginationResponse<WorkingCalendarResponse>> AddTimeWorkingAsync(
        
    //    CancellationToken cancellationToken)
    //{
    //    return await _workingCalendarService.GetAllNonAcceptWithPagination(filter, startDate, endDate, cancellationToken);
    //}
}
