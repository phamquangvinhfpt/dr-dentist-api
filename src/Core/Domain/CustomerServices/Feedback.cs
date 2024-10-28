using FSH.WebApi.Domain.Identity;

namespace FSH.WebApi.Domain.CustomerServices;

public class Feedback : AuditableEntity, IAggregateRoot
{
    public Guid? PatientProfileId { get; set; }
    public Guid? DoctorProfileId { get; set; }
    public Guid ServiceId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }

    // Navigation properties
    public PatientProfile? PatientProfile { get; set; }
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