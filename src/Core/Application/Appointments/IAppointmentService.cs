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
    Task<bool> CheckAvailableAppointment(string? patientId);
    Task<bool> CheckAppointmentExisting(Guid appointmentId);
    Task<PayAppointmentRequest> CreateAppointment(CreateAppointmentRequest request, CancellationToken cancellationToken);
    Task VerifyAndFinishBooking(PayAppointmentRequest request, CancellationToken cancellationToken);
    Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, CancellationToken cancellationToken);
    Task RescheduleAppointment(RescheduleRequest request, CancellationToken cancellationToken);
    Task CancelAppointment(CancelAppointmentRequest request, CancellationToken cancellationToken);
    Task ScheduleAppointment(ScheduleAppointmentRequest request, CancellationToken cancellationToken);
    Task<AppointmentResponse> GetAppointmentByID(Guid id, CancellationToken cancellationToken);
    Task<List<TreatmentPlanResponse>> ToggleAppointment(Guid id, CancellationToken cancellationToken);
    Task<PaymentDetailResponse> GetRemainingAmountOfAppointment(DefaultIdType id, CancellationToken cancellationToken);
    Task HandlePaymentRequest(PayAppointmentRequest request, CancellationToken cancellationToken);
    Task DoPaymentForAppointment(PayAppointmentRequest request, CancellationToken cancellationToken);
    Task<string> CancelPayment(PayAppointmentRequest request, CancellationToken cancellationToken);
}
