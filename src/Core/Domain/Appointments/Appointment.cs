using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;

namespace FSH.WebApi.Domain.Appointments;

public class Appointment : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DentistId { get; set; }
    public Guid? ServiceId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }

    // navigation
    public MedicalRecord? MedicalRecord { get; set; }
    public Payment? Payment { get; set; }

    public Appointment()
    {
    }

    public Appointment(string? patientId, string? dentistId, Guid? serviceId, DateTime appointmentDate, TimeSpan startTime, TimeSpan duration, AppointmentStatus status, string? notes)
    {
        PatientId = patientId;
        DentistId = dentistId;
        ServiceId = serviceId;
        AppointmentDate = appointmentDate;
        StartTime = startTime;
        Duration = duration;
        Status = status;
        Notes = notes;
    }
}