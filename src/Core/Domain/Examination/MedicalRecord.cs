using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Treatment;

namespace FSH.WebApi.Domain.Examination;

public class MedicalRecord : AuditableEntity, IAggregateRoot
{
    public Guid? DoctorProfileId { get; set; }
    public Guid? PatientProfileId { get; set; }
    public Guid? AppointmentId { get; set; }
    public DateTime Date { get; set; }

    // Navigation properties
    public DoctorProfile? DoctorProfile { get; set; }
    public PatientProfile? PatientProfile { get; set; }
    public Appointment? Appointment { get; set; }
    public Diagnosis? Diagnosis { get; set; }
    public Indication? Indication { get; set; }
    public BasicExamination? BasicExamination { get; set; }
    public Prescription? Prescription { get; set; }
    public ICollection<TreatmentPlanProcedures> TreatmentPlanProcedures { get; set; } = new List<TreatmentPlanProcedures>();
}