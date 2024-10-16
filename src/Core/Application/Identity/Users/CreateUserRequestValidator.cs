using FSH.WebApi.Shared.Authorization;

namespace FSH.WebApi.Application.Identity.Users;

public class CreateUserRequestValidator : CustomValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserService userService, ICurrentUser currentUser)
    {
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
        .MustAsync(async (birth, context, _) =>
        {
            return await userService.CheckBirthDayValid(birth.BirthDay, birth.Role);
        }).WithMessage((_, birthday) => $"Birthday {birthday} is unavailable.");

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

        //RuleFor(p => p.Job).Cascade(CascadeMode.Stop)
        //    .NotEmpty()
        //    .When(p => p.Role == FSHRoles.Patient)
        //    .WithMessage("Job is required for patients.");

        //RuleFor(p => p.Address).Cascade(CascadeMode.Stop)
        //    .NotEmpty()
        //    .When(p => p.Role == FSHRoles.Patient)
        //    .WithMessage("Address is required for patients.");

        RuleFor(p => p.DoctorProfile.Education).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => p.Role == FSHRoles.Dentist)
            .WithMessage("Education is required for Doctor.");

        RuleFor(p => p.DoctorProfile.Certification).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => p.Role == FSHRoles.Dentist)
            .WithMessage("Certification is required for Doctor.");

        RuleFor(p => p.DoctorProfile.YearOfExp).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => p.Role == FSHRoles.Dentist)
            .WithMessage("YearOfExp is required for Doctor.");

        RuleFor(p => p.DoctorProfile.SeftDescription).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => p.Role == FSHRoles.Dentist)
            .WithMessage("Seft-Description is required for Doctor.");

        RuleFor(p => p.Role).Cascade(CascadeMode.Stop)
            .MustAsync(async (_, role, context) =>
            {
                var r = true;
                if (role == FSHRoles.Staff || role == FSHRoles.Dentist)
                {
                    r = currentUser.IsInRole(FSHRoles.Admin);
                }
                return r;
            })
            .WithMessage("Only Admin can create Staff accounts.");

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