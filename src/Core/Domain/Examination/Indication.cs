namespace FSH.WebApi.Domain.Examination;

public class Indication : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DentistId { get; set; }
    public Guid? GeneralExaminationId { get; set; }
    public string[] IndicationType { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = string.Empty;

    // navigation
    public GeneralExamination? GeneralExamination { get; set; }
    public ICollection<PatientImage> Images { get; set; } = new List<PatientImage>();

    public Indication()
    {
    }

    public Indication(string? patientId, string? dentistId, Guid? generalExaminationId, string[] indicationType, string description)
    {
        PatientId = patientId;
        DentistId = dentistId;
        GeneralExaminationId = generalExaminationId;
        IndicationType = indicationType;
        Description = description;
    }
}