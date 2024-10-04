using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Treatment;

namespace FSH.WebApi.Domain.Examination;

public class GeneralExamination : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DentistId { get; set; }
    public Guid? AppointmentId { get; set; }
    public string ExamContent { get; set; } = string.Empty;
    public string TreatmentPlanNotes { get; set; } = string.Empty;

    // navigation
    public Appointment? Appointment { get; set; }
    public ICollection<Indication> Indications { get; set; } = new List<Indication>();
    public ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();
    public Payment? Payment { get; set; }
    public Prescription? Prescription { get; set; }

    public GeneralExamination()
    {
    }

    public GeneralExamination(string? patientId, string? dentistId, Guid? appointmentId, string examContent, string treatmentPlanNotes)
    {
        PatientId = patientId;
        DentistId = dentistId;
        AppointmentId = appointmentId;
        ExamContent = examContent;
        TreatmentPlanNotes = treatmentPlanNotes;
    }
}