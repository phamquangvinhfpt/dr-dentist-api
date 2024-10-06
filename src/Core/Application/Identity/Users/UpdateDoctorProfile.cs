using FluentValidation;
using FluentValidation.Results;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users;
public class UpdateDoctorProfile : IRequest<string>
{
    public string? DoctorID { get; set; }
    public string? Education { get; set; }
    public string? College { get; set; }
    public string? Certification { get; set; }
    public string? YearOfExp { get; set; }
    public string? SeftDescription { get; set; }
}
public class UpdateDoctorProfileVaidator : CustomValidator<UpdateDoctorProfile>
{
    public UpdateDoctorProfileVaidator(IUserService userService, ICurrentUser currentUser)
    {
        RuleFor(p => p)
            .Custom((profile, context) =>
            {
                if (!currentUser.IsInRole(FSHRoles.Admin) && !currentUser.IsInRole(FSHRoles.Dentist))
                {
                    context.AddFailure(new ValidationFailure(string.Empty, "Unauthorized access. Only Admin or Doctor can update doctor profile.")
                    {
                        ErrorCode = "Unauthorized"
                    });
                }
            });

        RuleFor(u => u.DoctorID).Cascade(CascadeMode.Stop)
            .NotEmpty()
                .WithMessage("Doctor is empty.")
            .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
                .WithMessage((_, id) => $"Doctor {id} is unavailable.");

        RuleFor(p => p.Education)
                .NotEmpty().WithMessage("Education is required for Doctor.");

        RuleFor(p => p.Certification)
            .NotEmpty().WithMessage("Certification is required for Doctor.");

        RuleFor(p => p.YearOfExp)
            .NotEmpty().WithMessage("YearOfExp is required for Doctor.");

        RuleFor(p => p.SeftDescription)
            .NotEmpty().WithMessage("SeftDescription is required for Doctor.");
    }
}

public class UpdateDoctorProfileHandler : IRequestHandler<UpdateDoctorProfile, string>
{
    private readonly IUserService _userService;
    private readonly IStringLocalizer<UpdateDoctorProfile> _t;

    public UpdateDoctorProfileHandler(IUserService userService, IStringLocalizer<UpdateDoctorProfile> t)
    {
        _userService = userService;
        _t = t;
    }

    public async Task<string> Handle(UpdateDoctorProfile request, CancellationToken cancellationToken)
    {
        await _userService.UpdateDoctorProfile(request);
        return _t["Profile updated successfully."];
    }
}
