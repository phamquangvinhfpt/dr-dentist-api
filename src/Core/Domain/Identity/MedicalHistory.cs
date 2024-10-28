namespace FSH.WebApi.Domain.Identity;
public class MedicalHistory : AuditableEntity, IAggregateRoot
{
    public Guid? PatientProfileId { get; set; }
    public string[] MedicalName { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }

    // Navigation property
    public PatientProfile? PatientProfile { get; set; }

    public MedicalHistory()
    {
    }

    public MedicalHistory(Guid? patientProfileId, string[] medicalName, string? note)
    {
        PatientProfileId = patientProfileId;
        MedicalName = medicalName;
        Note = note;
    }
}
