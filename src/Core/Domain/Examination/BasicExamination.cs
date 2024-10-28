namespace FSH.WebApi.Domain.Examination;

public class BasicExamination : AuditableEntity, IAggregateRoot
{
    public Guid? RecordId { get; set; }
    public string? ExaminationContent { get; set; }
    public string? TreatmentPlanNote { get; set; }

    // navigation
    public MedicalRecord? MedicalRecord { get; set; }

    public BasicExamination()
    {
    }

    public BasicExamination(Guid? recordId, string? examinationContent, string? treatmentPlanNote)
    {
        RecordId = recordId;
        ExaminationContent = examinationContent;
        TreatmentPlanNote = treatmentPlanNote;
    }
}