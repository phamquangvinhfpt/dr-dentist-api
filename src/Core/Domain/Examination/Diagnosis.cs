namespace FSH.WebApi.Domain.Examination;

public class Diagnosis : AuditableEntity, IAggregateRoot
{
    public Guid? RecordId { get; set; }
    public int ToothNumber { get; set; }
    public string[] TeethConditions { get; set; } = Array.Empty<string>();

    // navigation
    public MedicalRecord? MedicalRecord { get; set; }

    public Diagnosis()
    {
    }

    public Diagnosis(Guid? recordId, int toothNumber, string[] teethConditions)
    {
        RecordId = recordId;
        ToothNumber = toothNumber;
        TeethConditions = teethConditions;
    }
}