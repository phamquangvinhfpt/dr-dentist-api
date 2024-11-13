namespace FSH.WebApi.Domain.CustomerServices;

public class PatientMessages : AuditableEntity, IAggregateRoot
{
    public string? SenderId { get; set; }
    public string? receiverId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool isStaffSender { get; set; } = false;
    public bool IsRead { get; set; } = false;

    public PatientMessages()
    {
    }

    public PatientMessages(string senderId, string receiverId, string message, bool isStaffSender)
    {
        SenderId = senderId;
        this.receiverId = receiverId;
        Message = message;
        this.isStaffSender = isStaffSender;
    }
}