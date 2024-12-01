namespace FSH.WebApi.Domain.CustomerServices;

public class PatientMessages : AuditableEntity, IAggregateRoot
{
    public string? SenderId { get; set; }
    public string? ReceiverId { get; set; }
    public string? Message { get; set; } = string.Empty;
    public string[] Images { get; set; } = Array.Empty<string>();
    public bool IsRead { get; set; } = false;

    public PatientMessages()
    {
    }

    public PatientMessages(string senderId, string receiverId, string message)
    {
        SenderId = senderId;
        ReceiverId = receiverId;
        Message = message;
    }
}