namespace FSH.WebApi.Domain.Identity;
public class MedicalHistory : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string[] MedicalName { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }

    public MedicalHistory()
    {
    }

    public MedicalHistory(string? patientId, string[] medicalName, string? note)
    {
        PatientId = patientId;
        MedicalName = medicalName;
        Note = note;
    }
}
