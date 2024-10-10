using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Shared.Authorization;

namespace FSH.WebApi.Application.Identity.Users;

public class UpdateUserRequest : IRequest<string>
{
    public string? UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Job { get; set; }
    public string? Address { get; set; }
    public UpdateMedicalHistoryRequest? MedicalHistory { get; set; }
    public UpdatePatientFamilyRequest? PatientFamily { get; set; }
    public UpdateDoctorProfile? DoctorProfile { get; set; }
}

public class UpdateUserRequestValidator : CustomValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator(IUserService userService, ICurrentUser currentUser, IStringLocalizer<UpdateUserRequestValidator> T)
    {
        RuleFor(u => u.UserId).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (id, _) => (currentUser.GetUserId().ToString() != id))
                .WithMessage($"Only Update for Personal.");

        RuleFor(u => u.Email).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress()
                .WithMessage("Invalid Email Address.")
            .MustAsync(async (email, _) => !await userService.ExistsWithEmailAsync(email))
                .WithMessage((_, email) => $"Email {email} is already registered.");

        RuleFor(u => u.PhoneNumber).Cascade(CascadeMode.Stop)
            .MustAsync(async (phone, _) => !await userService.ExistsWithPhoneNumberAsync(phone!))
                .WithMessage((_, phone) => $"Phone number {phone} is already registered.")
                .Unless(u => string.IsNullOrWhiteSpace(u.PhoneNumber));

        RuleFor(p => p.FirstName)
           .MaximumLength(75);

        RuleFor(p => p.LastName)
            .MaximumLength(75);

        RuleFor(p => p.BirthDate)
        .Cascade(CascadeMode.Stop)
        .NotEmpty().WithMessage("Birth day is required.")
        .Must(p => p.HasValue).WithMessage("Birth day must be a valid date in the format dd-MM-yyyy.")
        .MustAsync(async (birth, context, _) =>
        {
            return await userService.CheckBirthDayValid(birth.BirthDate, currentUser.GetRole());
        }).WithMessage((_, birthday) => $"Birthday {birthday} is unavailable.");

        RuleFor(p => p.Job).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => !(currentUser.GetRole() == FSHRoles.Patient))
            .WithMessage("Job is required for patients.");

        RuleFor(p => p.Address).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .When(p => !(currentUser.GetRole() == FSHRoles.Patient))
            .WithMessage("Address is required for patients.");
    }
}

public class UpdateUserRequestHandler : IRequestHandler<UpdateUserRequest, string>
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<UpdateUserRequestHandler> _t;

    public UpdateUserRequestHandler(IUserService userService, IStringLocalizer<UpdateUserRequestHandler> t)
    {
        _userService = userService;
        _t = t;
    }

    public async Task<string> Handle(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.GetAsync(request.UserId!, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException(_t["User not found."]);
        }

        await _userService.UpdateAsync(request);
        return _t["Profile updated successfully."];
    }
}