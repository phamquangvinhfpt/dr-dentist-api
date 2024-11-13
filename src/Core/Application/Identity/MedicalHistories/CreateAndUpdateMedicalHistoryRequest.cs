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
        RuleFor(p => p.PatientId)
            .NotNull()
            .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Patient))
            .WithMessage((_, id) => $"User {id} is not patient.");

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
