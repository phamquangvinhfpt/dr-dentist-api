namespace FSH.WebApi.Domain.Examination;

public class Diagnosis : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public Guid? GeneralExaminationId { get; set; }
    public int ToothNumber { get; set; }
    public string[] TeethConditions { get; set; } = Array.Empty<string>();

    // navigation
    public GeneralExamination? GeneralExamination { get; set; }

    public Diagnosis()
    {
    }

    public Diagnosis(string? patientId, Guid? generalExaminationId, int toothNumber, string[] teethConditions)
    {
        PatientId = patientId;
        GeneralExaminationId = generalExaminationId;
        ToothNumber = toothNumber;
        TeethConditions = teethConditions;
    }
}