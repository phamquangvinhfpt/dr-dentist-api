using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Treatment;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Appointments;

public class Appointment : AuditableEntity, IAggregateRoot
{
    public Guid PatientId { get; set; }
    public Guid DentistId { get; set; }
    public Guid ServiceId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public AppointmentStatus Status { get; set; }
    public string? Notes { get; set; }
    public int SpamCount { get; set; } = 0;

    // navigation
    [JsonIgnore]
    public MedicalRecord? MedicalRecord { get; set; }
    [JsonIgnore]
    public Payment? Payment { get; set; }

    [JsonIgnore]
    public ICollection<TreatmentPlanProcedures> TreatmentPlanProcedures { get; set; } = new List<TreatmentPlanProcedures>();
    public Appointment()
    {
    }

    public Appointment(Guid patientId, Guid dentistId, Guid serviceId, DateOnly appointmentDate, TimeSpan startTime, TimeSpan duration, AppointmentStatus status, string? notes)
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