namespace FSH.WebApi.Domain.CustomerServices;

public class ContactInfor : AuditableEntity, IAggregateRoot
{
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public ContactInfor()
    {
    }

    public ContactInfor(string title, string email, string phone, string content)
    {
        Title = title;
        Email = email;
        Phone = phone;
        Content = content;
    }
}