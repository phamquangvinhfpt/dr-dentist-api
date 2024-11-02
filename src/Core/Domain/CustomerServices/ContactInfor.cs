namespace FSH.WebApi.Domain.CustomerServices;

public class ContactInfor : AuditableEntity, IAggregateRoot
{
    public string? StaffId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public ContactInfor()
    {
    }

    public ContactInfor(string? staffId, string title, string email, string phone, string content)
    {
        StaffId = staffId;
        Title = title;
        Email = email;
        Phone = phone;
        Content = content;
    }
}