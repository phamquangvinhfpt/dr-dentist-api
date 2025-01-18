using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Application.TreatmentPlan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public interface IAppointmentService : ITransientService
{
    Task<bool> CheckAppointmentAvailableToReschedule(Guid appointmentId);
    Task<bool> CheckAppointmentDateValid(DateOnly date);
    Task<bool> CheckAvailableAppointment(string? patientId, DateOnly appointmentDate);
    Task<bool> CheckAppointmentExisting(Guid appointmentId);
    Task<PayAppointmentRequest> CreateAppointment(CreateAppointmentRequest request, CancellationToken cancellationToken);
    Task VerifyAndFinishBooking(PayAppointmentRequest request, CancellationToken cancellationToken);
    Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken);
    Task RescheduleAppointment(RescheduleRequest request, CancellationToken cancellationToken);
    Task CancelAppointment(CancelAppointmentRequest request, CancellationToken cancellationToken);
    Task ScheduleAppointment(ScheduleAppointmentRequest request, CancellationToken cancellationToken);
    Task<AppointmentResponse> GetAppointmentByID(Guid id, CancellationToken cancellationToken);
    Task<List<TreatmentPlanResponse>> ToggleAppointment(Guid id, CancellationToken cancellationToken);
    Task<PaymentDetailResponse> GetRemainingAmountOfAppointment(DefaultIdType id, CancellationToken cancellationToken);
    Task HandlePaymentRequest(PayAppointmentRequest request, CancellationToken cancellationToken);
    Task DoPaymentForAppointment(PayAppointmentRequest request, CancellationToken cancellationToken);
    Task<string> CancelPayment(string code, CancellationToken cancellationToken);
    Task<PaginationResponse<AppointmentResponse>> GetNonDoctorAppointments(PaginationFilter filter, DateOnly date, TimeSpan time, CancellationToken cancellationToken);
    Task<string> AddDoctorToAppointments(AddDoctorToAppointment request, CancellationToken cancellationToken);
    Task<PaginationResponse<GetWorkingDetailResponse>> GetFollowUpAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken);
    Task<PaginationResponse<GetWorkingDetailResponse>> GetReExamAppointments(PaginationFilter filter, DateOnly date, CancellationToken cancellationToken);
    Task<string> CreateReExamination(AddReExamination request, CancellationToken cancellationToken);
    Task<List<GetDoctorResponse>> GetAvailableDoctorAsync(GetAvailableDoctor request, CancellationToken cancellationToken);
    Task<string> ToggleFollowAppointment(DefaultIdType id, CancellationToken cancellationToken);
    Task<string> RevertPayment(DefaultIdType id);
    Task JobAppointmentsAsync();
    Task DeleteRedisCode();
    Task SendHubJob(DateOnly date, string patient, string docter, string role);
    Task JobFollowAppointmentsAsync();
}
