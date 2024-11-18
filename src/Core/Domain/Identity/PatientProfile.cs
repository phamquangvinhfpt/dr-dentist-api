using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Treatment;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Identity;

public class PatientProfile : AuditableEntity, IAggregateRoot
{
    public string? UserId { get; set; }
    public string? PatientCode { get; set; }
    public string? IDCardNumber { get; set; }
    public string? Occupation { get; set; }

    // Navigation property
    [JsonIgnore]
    public PatientFamily? PatientFamily { get; set; }
    [JsonIgnore]
    public MedicalHistory? MedicalHistory { get; set; }
    [JsonIgnore]
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    [JsonIgnore]
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    [JsonIgnore]
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    [JsonIgnore]
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}