using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class UserProfileResponse
{
    public string Id { get; set; }
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
    public string? Role { get; set; }
    public PatientProfile? PatientProfile { get; set; }
    public MedicalHistory? MedicalHistory { get; set; }
    public PatientFamily? PatientFamily { get; set; }
    public DoctorProfile? DoctorProfile { get; set; }
}
