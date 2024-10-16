namespace FSH.WebApi.Domain.CustomerServices;

public class Feedback : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DoctorId { get; set; }
    public Guid ServiceId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }

    public Feedback()
    {
    }

    public Feedback(string? patientId, string? doctorId, Guid serviceId, string message, int rating)
    {
        PatientId = patientId;
        DoctorId = doctorId;
        ServiceId = serviceId;
        Message = message;
        Rating = rating;
    }
}