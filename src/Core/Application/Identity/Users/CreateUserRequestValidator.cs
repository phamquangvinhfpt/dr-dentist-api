using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Shared.Authorization;

namespace FSH.WebApi.Application.Identity.Users;

public class CreateUserRequestValidator : CustomValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IUserService userService, ICurrentUser currentUser, IServiceService serviceService)
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
            .Matches(@"^(0|84|\\+84)(3|5|7|8|9)[0-9]{8}$")
            .WithMessage("Invalid phone number format. Please enter a valid Vietnamese phone number.")
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

        RuleFor(p => p.DoctorProfile).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => p.Role == FSHRoles.Dentist)
            .SetValidator(new UpdateDoctorProfileVaidator(userService, currentUser, serviceService))
            .When(p => p.Role == FSHRoles.Dentist);

        RuleFor(p => p.Role).Cascade(CascadeMode.Stop)
            .MustAsync(async (_, role, context) =>
            {
                bool r = true;
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