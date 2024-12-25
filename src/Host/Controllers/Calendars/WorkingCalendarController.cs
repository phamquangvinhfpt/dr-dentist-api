using DocumentFormat.OpenXml.Wordprocessing;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Domain.Appointments;
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

    [HttpGet("get/non-calendar")]
    [OpenApiOperation("Get Doctors has non calendar in this month.", "")]
    public async Task<List<GetDoctorResponse>> RegisterFullTimeWorkingAsync(
        [FromQuery] DateTime date,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetAllDoctorHasNonCalendar(date, cancellationToken);
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

    [HttpPost("add-room/auto")]
    [OpenApiOperation("Auto Add Room to Working Calendar.", "")]
    public async Task<string> AutoAddRoomToWorkingAsync(
        List<Guid> request,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.AutoAddRoomForWorkingAsync(request, cancellationToken); ;
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
    public async Task<PaginationResponse<RoomDetail>> GetRoomsAsync(
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

    [HttpPost("time/get")]
    [OpenApiOperation("Get Time Working of Doctor", "")]
    public async Task<List<TimeDetail>> GetTimeWorkingAsync(
        GetTimeWorkingRequest request,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.GetTimeWorkingAsync(request, cancellationToken);
    }

    [HttpPost("part-time/reminder/{id}")]
    [OpenApiOperation("Send notification reminder to part time.", "")]
    public async Task<string> SendNotiReminderAsync(
        string id,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        return await _workingCalendarService.SendNotiReminderAsync(id, date, cancellationToken);
    }

    [HttpPost("export-working-calendar")]
    [OpenApiOperation("Export working calendar logs.", "")]
    [MustHavePermission(FSHAction.Export, FSHResource.Files)]
    public async Task<FileResult> ExportWorkingCalendarAsync([FromQuery] DateOnly start, [FromQuery] DateOnly end, [FromQuery] string DoctorID)
    {
        var stream = await _workingCalendarService.ExportWorkingCalendarAsync(start, end, DoctorID);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"working_calendar_export{start.Month}{start.Year}{end.Month}{end.Year}.xlsx");
    }
}
