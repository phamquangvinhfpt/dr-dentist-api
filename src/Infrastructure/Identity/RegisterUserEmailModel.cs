namespace FSH.WebApi.Infrastructure.Identity;

public class RegisterUserEmailModel
{
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? Password { get; set; }
    public string BanReason { get; set; } = default!;
}