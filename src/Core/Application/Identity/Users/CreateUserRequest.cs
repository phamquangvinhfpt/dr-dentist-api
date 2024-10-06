using FSH.WebApi.Shared.Authorization;

namespace FSH.WebApi.Application.Identity.Users;

public class CreateUserRequest
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public bool IsMale { get; set; } = true;
    public DateOnly? BirthDay { get; set; }
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? Job { get; set; }
    public string? Address { get; set; }
    public UpdateDoctorProfile? DoctorProfile { get; set; }
    public string? Role { get; set; } = FSHRoles.Patient;
}