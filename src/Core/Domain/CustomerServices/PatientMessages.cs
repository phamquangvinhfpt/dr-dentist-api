namespace FSH.WebApi.Domain.CustomerServices;

public class PatientMessages : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public string? StaffId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;

    public PatientMessages()
    {
    }

    public PatientMessages(string? patientId, string? staffID, string message, bool isRead)
    {
        PatientId = patientId;
        StaffId = staffID;
        Message = message;
        IsRead = isRead;
    }
}