using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Treatment;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Examination;

public class MedicalRecord : AuditableEntity, IAggregateRoot
{
    public Guid? DoctorProfileId { get; set; }
    public Guid? PatientProfileId { get; set; }
    public Guid? AppointmentId { get; set; }
    public DateTime Date { get; set; }

    // Navigation properties
    [JsonIgnore]
    public DoctorProfile? DoctorProfile { get; set; }
    [JsonIgnore]
    public PatientProfile? PatientProfile { get; set; }
    [JsonIgnore]
    public Appointment? Appointment { get; set; }
    [JsonIgnore]
    public Diagnosis? Diagnosis { get; set; }
    [JsonIgnore]
    public Indication? Indication { get; set; }
    [JsonIgnore]
    public BasicExamination? BasicExamination { get; set; }
    [JsonIgnore]
    public Prescription? Prescription { get; set; }
    [JsonIgnore]
    public ICollection<TreatmentPlanProcedures> TreatmentPlanProcedures { get; set; } = new List<TreatmentPlanProcedures>();
}