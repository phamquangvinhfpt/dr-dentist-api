using FSH.WebApi.Domain.Identity;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.CustomerServices;

public class Feedback : AuditableEntity, IAggregateRoot
{
    public Guid? PatientProfileId { get; set; }
    public Guid? DoctorProfileId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid AppointmentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }

    // Navigation properties
    [JsonIgnore]
    public PatientProfile? PatientProfile { get; set; }
    [JsonIgnore]
    public DoctorProfile? DoctorProfile { get; set; }

    public Feedback()
    {
    }

    public Feedback(Guid? patientProfileId, Guid? doctorProfileId, Guid serviceId, string message, int rating)
    {
        PatientProfileId = patientProfileId;
        DoctorProfileId = doctorProfileId;
        ServiceId = serviceId;
        Message = message;
        Rating = rating;
    }
}