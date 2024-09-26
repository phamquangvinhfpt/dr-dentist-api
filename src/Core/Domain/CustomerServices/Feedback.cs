namespace FSH.WebApi.Domain.CustomerServices;

public class Feedback : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? DoctorId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Rating { get; set; }

    public Feedback()
    {
    }

    public Feedback(string? patientId, string? doctorId, string message, int rating)
    {
        PatientId = patientId;
        DoctorId = doctorId;
        Message = message;
        Rating = rating;
    }
}