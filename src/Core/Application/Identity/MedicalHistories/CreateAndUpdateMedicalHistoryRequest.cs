using FluentValidation.Results;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.MedicalHistories;
public class CreateAndUpdateMedicalHistoryRequest : IRequest<string>
{
    public string? PatientId { get; set; }
    public string[] MedicalName { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }
}
public class CreateAndUpdateMedicalHistoryVaidator : CustomValidator<CreateAndUpdateMedicalHistoryRequest>
{
    public CreateAndUpdateMedicalHistoryVaidator(IUserService userService, ICurrentUser currentUser)
    {
        RuleFor(p => p)
            .CustomAsync(async (profile, context, cancellationToken) =>
            {
                if (currentUser.IsInRole(FSHRoles.Admin) || currentUser.IsInRole(FSHRoles.Staff))
                {
                    return;
                }

                if (currentUser.IsInRole(FSHRoles.Patient))
                {
                    if (currentUser.GetUserId().ToString() != profile.PatientId)
                    {
                        context.AddFailure(new ValidationFailure(nameof(profile.PatientId),
                            "You can only update your own Medical History.")
                        {
                            ErrorCode = "Unauthorized"
                        });
                    }
                    if (!await userService.ExistsWithUserIDAsync(profile.PatientId))
                    {
                        context.AddFailure(new ValidationFailure(nameof(profile.PatientId),
                            $"Doctor {profile.PatientId} is unavailable.")
                        {
                            ErrorCode = "BadRequest"
                        });
                    }
                    return;
                }

                context.AddFailure(new ValidationFailure(string.Empty,
                    "Unauthorized access. Doctor can not update patient medical history.")
                {
                    ErrorCode = "Unauthorized"
                });
            });

        RuleFor(p => p.MedicalName)
                .NotEmpty().WithMessage("Medical Name is required.");
    }
}

public class CreateAndUpdateMedicalHistoryRequestHandler : IRequestHandler<CreateAndUpdateMedicalHistoryRequest, string>
{
    private readonly IMedicalHistoryService _medicalHistoryService;
    private readonly IStringLocalizer<CreateAndUpdateMedicalHistoryRequest> _t;

    public CreateAndUpdateMedicalHistoryRequestHandler(IStringLocalizer<CreateAndUpdateMedicalHistoryRequest> t, IMedicalHistoryService medicalHistoryService)
    {
        _medicalHistoryService = medicalHistoryService;
        _t = t;
    }

    public async Task<string> Handle(CreateAndUpdateMedicalHistoryRequest request, CancellationToken cancellationToken)
    {
        await _medicalHistoryService.CreateAndUpdateMedicalHistory(request, cancellationToken);
        return _t["Medical History updated successfully."];
    }
}
