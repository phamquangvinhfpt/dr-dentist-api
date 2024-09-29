using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class SeftRegistNewPatientValidator : CustomValidator<SeftRegistNewPatient>
{
    public SeftRegistNewPatientValidator(IUserService userService) {
        RuleFor(u => u.Email).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress()
                .WithMessage("Invalid Email Address.")
            .MustAsync(async (email, _) => !await userService.ExistsWithEmailAsync(email))
                .WithMessage((_, email) => $"Email {email} is already registered.");
        RuleFor(p => p.BirthDay)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Birth day is required.")
            .Must(p => p.HasValue).WithMessage("Birth day must be a valid date in the format dd-MM-yyyy.")
            .Must(p => p.Value < DateOnly.FromDateTime(DateTime.Today).AddYears(-18)).WithMessage("Birth day must be valid");
        RuleFor(u => u.UserName).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(6);

        RuleFor(u => u.PhoneNumber).Cascade(CascadeMode.Stop)
            .MustAsync(async (phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!))
                .WithMessage((_, phone) => $"Phone number {phone} is already registered.")
                .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));

        RuleFor(p => p.FirstName).Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.LastName).Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.Job).Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.Address).Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.Password).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$")
            .WithMessage("Password must contain at least one uppercase letter, one lowercase letter, one number and one special character");

        RuleFor(p => p.ConfirmPassword).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Equal(p => p.Password);
    }
}
