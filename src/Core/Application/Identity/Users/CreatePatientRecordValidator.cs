using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class CreatePatientRecordValidator : CustomValidator<CreatePatientRecord>
{
    public CreatePatientRecordValidator(IUserService userService)
    {
        RuleFor(p => p.PatientId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Patient is not available.")
            .MustAsync(async (patientID, _) => !await userService.ExistsWithUserIDAsync(patientID))
            .WithMessage((_, patientID) => $"User {patientID} is not existed.");

        RuleFor(p => p.BirthDay)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Birth day is required.")
            .Must(p => p.HasValue).WithMessage("Birth day must be a valid date in the format dd-MM-yyyy.")
            .Must(p => p.Value < DateOnly.FromDateTime(DateTime.Today).AddYears(-18)).WithMessage("Birth day must be valid");


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
    }
}
