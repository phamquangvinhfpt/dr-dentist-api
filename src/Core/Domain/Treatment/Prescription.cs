using FSH.WebApi.Domain.Appointments;

namespace FSH.WebApi.Domain.Treatment;

public class Prescription : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DentistId { get; set; }
    public Guid? AppointmentId { get; set; }
    public string? Notes { get; set; }

    // navigation property
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public Appointment? Appointment { get; set; }

    public Prescription()
    {
    }

    public Prescription(string? patientId, string? dentistId, Guid? appointmentId, string notes)
    {
        PatientId = patientId;
        DentistId = dentistId;
        AppointmentId = appointmentId;
        Notes = notes;
    }
}