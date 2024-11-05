using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public interface IAppointmentService : ITransientService
{
    Task<bool> CheckAppointmentDateValid(DateOnly date);
    Task<bool> CheckAvailableAppointment(string? patientId);
    Task<bool> CheckAppointmentExisting(Guid appointmentId);
    Task<AppointmentDepositRequest> CreateAppointment(CreateAppointmentRequest request, CancellationToken cancellationToken);
    Task VerifyAndFinishBooking(AppointmentDepositRequest request, CancellationToken cancellationToken);
}
