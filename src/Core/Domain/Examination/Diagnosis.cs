using FSH.WebApi.Domain.Treatment;

namespace FSH.WebApi.Domain.Examination;

public class Diagnosis : AuditableEntity, IAggregateRoot
{
    public Guid? GeneralExaminationId { get; set; }
    public int ToothNumber { get; set; }
    public string[] TeethConditions { get; set; } = Array.Empty<string>();

    // navigation
    public GeneralExamination? GeneralExamination { get; set; }
    public ICollection<TreatmentPlanProcedures> TreatmentPlanProcedures { get; set; } = new List<TreatmentPlanProcedures>();

    public Diagnosis()
    {
    }

    public Diagnosis(Guid? generalExaminationId, int toothNumber, string[] teethConditions)
    {
        GeneralExaminationId = generalExaminationId;
        ToothNumber = toothNumber;
        TeethConditions = teethConditions;
    }
}