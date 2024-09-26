using FSH.WebApi.Domain.Examination;

namespace FSH.WebApi.Domain.Treatment;

public class TreatmentPlan : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DentistId { get; set; }
    public Guid? DiagnosisId { get; set; }
    public Guid? GeneralExaminationId { get; set; }
    public bool PatientAcceptance { get; set; }

    // navigation property
    public ICollection<TreatmentPlanProcedures> Procedures { get; set; } = new List<TreatmentPlanProcedures>();
    public GeneralExamination? GeneralExamination { get; set; }

    public TreatmentPlan()
    {
    }

    public TreatmentPlan(string? patientId, string? dentistId, Guid? diagnosisId, Guid? generalExaminationId, bool patientAcceptance)
    {
        PatientId = patientId;
        DentistId = dentistId;
        DiagnosisId = diagnosisId;
        GeneralExaminationId = generalExaminationId;
        PatientAcceptance = patientAcceptance;
    }
}
