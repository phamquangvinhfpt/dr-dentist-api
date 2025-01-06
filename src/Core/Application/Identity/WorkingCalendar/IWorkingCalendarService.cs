using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Domain.Examination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public interface IWorkingCalendarService : ITransientService
{
    Task<string> CreateWorkingCalendarForParttime(List<CreateOrUpdateWorkingCalendar> request, string doctorID, CancellationToken cancellationToken);
    Task<PaginationResponse<WorkingCalendarResponse>> GetWorkingCalendarPagination(PaginationFilter filter, DateOnly date, DateOnly Edate, CancellationToken cancellationToken);
    Task<string> UpdateWorkingCalendar(List<CreateOrUpdateWorkingCalendar> request, string doctorID, CancellationToken cancellationToken);
    Task<bool> CheckAvailableTimeWorking(string DoctorID, DateOnly date, TimeSpan time);
    Task<string> FullTimeRegistDateWorking(string doctorID, DateTime date, CancellationToken cancellationToken);
    Task<string> AddRoomForWorkingAsync(AddRoomToWorkingRequest request, CancellationToken cancellationToken);
    Task<string> CreateRoomsAsync(AddRoomRequest request, CancellationToken cancellationToken);
    Task<PaginationResponse<RoomDetail>> GetRoomsWithPagination(PaginationFilter filter, CancellationToken cancellationToken);
    Task<PaginationResponse<WorkingCalendarResponse>> GetPartTimeNonAcceptWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<PaginationResponse<WorkingCalendarResponse>> GetFullTimeNonAcceptWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<PaginationResponse<WorkingCalendarResponse>> GetFullTimeOffWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<PaginationResponse<WorkingCalendarResponse>> GetPartTimeOffWorkingCalendarsAsync(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<PaginationResponse<WorkingCalendarResponse>> GetAllNonAcceptWithPagination(PaginationFilter filter, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<List<GetDoctorResponse>> GetAllDoctorHasNonCalendar(DateTime date, CancellationToken cancellationToken);
    Task<List<TimeDetail>> GetTimeWorkingAsync(GetTimeWorkingRequest request, CancellationToken cancellationToken);
    Task<string> AutoAddRoomForWorkingAsync(List<DefaultIdType> request, CancellationToken cancellationToken);
    Task<string> SendNotiReminderAsync(string id, DateOnly date, CancellationToken cancellationToken);
    Task<Stream> ExportWorkingCalendarAsync(DateOnly start, DateOnly end);
}
