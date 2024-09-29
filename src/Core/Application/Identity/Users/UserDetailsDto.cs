namespace FSH.WebApi.Application.Identity.Users;

public class UserDetailsDto
{
    public Guid Id { get; set; }

    public string? UserName { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public bool? Gender { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public bool EmailConfirmed { get; set; }

    public string? PhoneNumber { get; set; }

    public bool? PhoneNumberConfirmed { get; set; }
    public string? Job { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
}