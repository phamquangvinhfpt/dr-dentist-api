namespace FSH.WebApi.Domain.Identity;
public class PatientFamily : AuditableEntity, IAggregateRoot
{
    public Guid? PatientProfileId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public PatientFamilyRelationship Relationship { get; set; }

    // Navigation property
    public PatientProfile? PatientProfile { get; set; }

    public PatientFamily()
    {
    }

    public PatientFamily(Guid? patientProfileId, string? name, string? phone, string? email, PatientFamilyRelationship relationship)
    {
        PatientProfileId = patientProfileId;
        Name = name;
        Phone = phone;
        Email = email;
        Relationship = relationship;
    }
}