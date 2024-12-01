using FluentValidation;
using FluentValidation.Results;
using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Domain.Identity;
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
    public Guid TypeServiceID { get; set; }
    public string? Education { get; set; }
    public string? College { get; set; }
    public string? Certification { get; set; }
    public string? YearOfExp { get; set; }
    public string? SeftDescription { get; set; }
    public WorkingType WorkingType { get; set; }
}
public class UpdateDoctorProfileVaidator : CustomValidator<UpdateDoctorProfile>
{
    public UpdateDoctorProfileVaidator(IUserService userService, ICurrentUser currentUser, IServiceService serviceService)
    {
        RuleFor(p => p.TypeServiceID)
            .NotNull()
            .WithMessage("Type service should be choose")
            .MustAsync(async (id, _) => await serviceService.CheckTypeServiceExisting(id))
            .WithMessage((_, id) => "Type service is unavailable.");

        RuleFor(p => p.Education)
                .NotEmpty().WithMessage("Education is required for Doctor.");

        RuleFor(p => p.College)
                .NotEmpty().WithMessage("College is required for Doctor.");

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
        await _userService.UpdateDoctorProfile(request, cancellationToken);
        return _t["Profile updated successfully."];
    }
}
