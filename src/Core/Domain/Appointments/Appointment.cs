using FSH.WebApi.Domain.Examination;

namespace FSH.WebApi.Domain.Appointments;

public class Appointment : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DentistId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }

    // navigation
    public GeneralExamination? GeneralExamination { get; set; }

    public Appointment()
    {
    }

    public Appointment(string? PatientId, string? DentistId, DateTime appointmentDate, TimeSpan startTime, TimeSpan duration, AppointmentStatus status, string? notes)
    {
        PatientId = PatientId;
        DentistId = DentistId;
        AppointmentDate = appointmentDate;
        StartTime = startTime;
        Duration = duration;
        Status = status;
        Notes = notes;
    }
}