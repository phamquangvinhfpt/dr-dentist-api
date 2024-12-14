namespace FSH.WebApi.Domain.CustomerServices;

public class ContactInfor : AuditableEntity, IAggregateRoot
{
    public string? StaffId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? EmailContext { get; set; }
    public string[] ImageUrl { get; set; }
    public ContactStatus Status { get; set; }

    public ContactInfor()
    {
    }

    public ContactInfor(string? staffId, string title, string email, string phone, string content, string? emailContext, string[] imageUrl, ContactStatus status)
    {
        StaffId = staffId;
        Title = title;
        Email = email;
        Phone = phone;
        Content = content;
        EmailContext = emailContext;
        ImageUrl = imageUrl;
        Status = status;
    }
}