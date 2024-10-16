namespace FSH.WebApi.Domain.Identity;
public class PatientFamily : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public PatientFamilyRelationship Relationship { get; set; }

    public PatientFamily()
    {
    }

    public PatientFamily(string? patientId, string? name, string? phone, string? email, PatientFamilyRelationship relationship)
    {
        PatientId = patientId;
        Name = name;
        Phone = phone;
        Email = email;
        Relationship = relationship;
    }
}
