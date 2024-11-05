using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public interface IAppointmentService : ITransientService
{
    Task<bool> CheckAppointmentDateValid(DateTime date);
    Task<bool> CheckAvailableAppointment(string? patientId, Guid serviceId, DateTime appointmentDate, TimeSpan startTime, TimeSpan duration);
    Task<bool> CheckAvailableTimeSlot(string? dentistId, DateTime appointmentDate, TimeSpan startTime, TimeSpan duration);
}
