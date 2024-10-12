using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Shared.Authorization;
using MediatR;

namespace FSH.WebApi.Application.Identity.Users;

public class UpdateUserRequest : IRequest<string>
{
    public string? UserId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Gender { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Job { get; set; }
    public string? Address { get; set; }
    public CreateAndUpdateMedicalHistoryRequest? MedicalHistory { get; set; }
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
                .WithMessage($"Only Update for Personal.")
            .MustAsync(async (id, _) => (!await userService.ExistsWithUserIDAsync(id)))
                .WithMessage($"User not found.");

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

        //RuleFor(p => p.Job).Cascade(CascadeMode.Stop)
        //    .NotEmpty()
        //    .When(p => !(currentUser.GetRole() == FSHRoles.Patient))
        //    .WithMessage("Job is required for patients.");

        //RuleFor(p => p.Address).Cascade(CascadeMode.Stop)
        //    .NotEmpty()
        //    .When(p => !(currentUser.GetRole() == FSHRoles.Patient))
        //    .WithMessage("Address is required for patients.");
    }
}

public class UpdateUserRequestHandler : IRequestHandler<UpdateUserRequest, string>
{
    private readonly IUserService _userService;
    private readonly ICurrentUser _currentUser;
    private readonly IStringLocalizer<UpdateUserRequestHandler> _t;

    public UpdateUserRequestHandler(IUserService userService, IStringLocalizer<UpdateUserRequestHandler> t, ICurrentUser currentUser)
    {
        _userService = userService;
        _t = t;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var role = await _userService.GetRolesAsync(request.UserId, cancellationToken);
        if (role.RoleName == FSHRoles.Dentist) {
            var check = new UpdateDoctorProfileVaidator(_userService, _currentUser).ValidateAsync(request.DoctorProfile);
            if (check.IsCompleted)
            {
                var t = check.Result;
                if (!t.IsValid)
                {
                    throw new BadRequestException(t.Errors[0].ErrorMessage);
                }
            }
        }else if (role.RoleName == FSHRoles.Patient)
        {
            var check = new CreateAndUpdateMedicalHistoryVaidator(_userService, _currentUser).ValidateAsync(request.MedicalHistory);
            if (check.IsCompleted)
            {
                var t = check.Result;
                if (!t.IsValid)
                {
                    throw new BadRequestException(t.Errors[0].ErrorMessage);
                }
            }
        }
        await _userService.UpdateAsync(request);
        return _t["Profile updated successfully."];
    }
}