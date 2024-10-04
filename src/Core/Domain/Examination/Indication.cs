namespace FSH.WebApi.Domain.Examination;

public class Indication : AuditableEntity, IAggregateRoot
{
    public Guid? GeneralExaminationId { get; set; }
    public string[] IndicationType { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = string.Empty;

    // navigation
    public GeneralExamination? GeneralExamination { get; set; }
    public ICollection<PatientImage> Images { get; set; } = new List<PatientImage>();

    public Indication()
    {
    }

    public Indication(Guid? generalExaminationId, string[] indicationType, string description)
    {
        GeneralExaminationId = generalExaminationId;
        IndicationType = indicationType;
        Description = description;
    }
}