using Microsoft.AspNetCore.Identity;

namespace FSH.WebApi.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }
    public string? Job { get; set; }
    public string? Address { get; set; }
    public string? ObjectId { get; set; }
}