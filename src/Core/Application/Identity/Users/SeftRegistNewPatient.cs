using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class SeftRegistNewPatient
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
    public string? Job {  get; set; }
    public string Address { get; set; } = default!;
}
