using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;

namespace FSH.WebApi.Domain.Identity;

public class PatientProfile : AuditableEntity, IAggregateRoot
{
    public string? UserId { get; set; }
    public string? PatientCode { get; set; }
    public string? IDCardNumber { get; set; }
    public string? Occupation { get; set; }

    // Navigation property
    public PatientFamily? PatientFamily { get; set; }
    public MedicalHistory? MedicalHistory { get; set; }
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}