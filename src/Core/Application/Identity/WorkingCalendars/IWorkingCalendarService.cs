using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendars;
public interface IWorkingCalendarService : ITransientService
{
    List<WorkingCalendar> CreateWorkingCalendar(Guid doctorId, TimeSpan startTime, TimeSpan endTime, string? note = null);
    Task<PaginationResponse<WorkingCalendarResponse>> GetWorkingCalendars(PaginationFilter filter, DateOnly date, CancellationToken cancellation);
    Task<bool> CheckAvailableTimeSlot(DateOnly date,TimeSpan start, TimeSpan end, string DoctorID);
    Task<bool> CheckAvailableTimeSlotToReschedule(Guid appointmentID, DateOnly appointmentDate, TimeSpan startTime, TimeSpan endTime);
    Task<List<AvailableTimeResponse>> GetAvailableTimeSlot(GetAvailableTimeRequest request, CancellationToken cancellationToken);
    Task<bool> CheckAvailableTimeSlotToAddFollowUp(Guid doctorID, DateOnly treatmentDate, TimeSpan treatmentTime);
    Task<GetWorkingDetailResponse> GetCalendarDetail(Guid id, CancellationToken cancellationToken);
    Task<bool> CheckAvailableTimeSlot(DateOnly date, TimeSpan start, TimeSpan end, Guid DoctorID);
}
