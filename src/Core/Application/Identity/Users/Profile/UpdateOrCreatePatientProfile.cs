using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.Users.Profile;
public class UpdateOrCreatePatientProfile : IRequest<string>
{
    public Guid? PatientProfileId { get; set; }
    public bool IsUpdateProfile { get; set; } = false;
    public PatientProfileRequest? Profile { get; set; }
    public bool IsUpdateMedicalHistory { get; set; } = false;
    public MedicalHistoryRequest? MedicalHistory { get; set; }
    public bool IsUpdatePatientFamily { get; set; } = false;
    public PatientFamilyRequest? PatientFamily { get; set; }
}
public class UpdateOrCreatePatientProfileValidator : CustomValidator<UpdateOrCreatePatientProfile>
{
    public UpdateOrCreatePatientProfileValidator(IUserService userService)
    {
        RuleFor(x => x.PatientProfileId)
            .NotEmpty()
            .When(x => x.IsUpdateMedicalHistory || x.IsUpdatePatientFamily);

        RuleFor(x => x.Profile)
            .SetValidator(new PatientProfileRequestValidator(userService))
            .When(x => x.IsUpdateProfile);

        RuleFor(x => x.MedicalHistory)
            .SetValidator(new MedicalHistoryRequestValidator())
            .When(x => x.IsUpdateMedicalHistory);

        RuleFor(x => x.PatientFamily)
            .SetValidator(new PatientFamilyRequestValidator())
            .When(x => x.IsUpdatePatientFamily);
    }
}
public class UpdateOrCreatePatientProfileHandler : IRequestHandler<UpdateOrCreatePatientProfile, string>
{
    private readonly IUserService _userService;
    private readonly ICurrentUser _currentUser;
    private readonly IStringLocalizer<UpdateOrCreatePatientProfileHandler> _t;

    public UpdateOrCreatePatientProfileHandler(IUserService userService, IStringLocalizer<UpdateOrCreatePatientProfileHandler> t, ICurrentUser currentUser)
    {
        _userService = userService;
        _t = t;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(UpdateOrCreatePatientProfile request, CancellationToken cancellationToken)
    {
        await _userService.UpdateOrCreatePatientProfile(request, cancellationToken);
        return _t["Successfully"];
    }
}
