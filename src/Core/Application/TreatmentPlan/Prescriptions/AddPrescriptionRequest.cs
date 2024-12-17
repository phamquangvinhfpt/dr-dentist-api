using FSH.WebApi.Application.Appointments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.TreatmentPlan.Prescriptions;
public class AddPrescriptionRequest : IRequest<string>
{
    public Guid TreatmentID { get; set; }
    public string? Notes { get; set; }
    public List<AddPrescriptionItemRequest>? ItemRequests { get; set; }
}

public class AddPrescriptionRequestValidator : CustomValidator<AddPrescriptionRequest>
{
    public AddPrescriptionRequestValidator()
    {
        RuleFor(p => p.TreatmentID)
            .NotEmpty()
            .WithMessage("Treatment is required");

        RuleFor(p => p.ItemRequests)
            .NotNull()
            .WithMessage("Prescription items are required")
            .Must(items => items != null && items.Any())
            .WithMessage("At least one prescription item is required");

        RuleForEach(p => p.ItemRequests)
            .SetValidator(new AddPrescriptionItemRequestValidator())
            .When(p => p.ItemRequests != null);

        When(p => !string.IsNullOrEmpty(p.Notes), () =>
        {
            RuleFor(p => p.Notes)
                .MaximumLength(1000)
                .WithMessage("Notes cannot exceed 1000 characters");
        });
    }
}

public class AddPrescriptionRequestHandler : IRequestHandler<AddPrescriptionRequest, string>
{
    private readonly ITreatmentPlanService _treatmentPlanService;
    private readonly IStringLocalizer<AddPrescriptionRequest> _t;

    public AddPrescriptionRequestHandler(ITreatmentPlanService treatmentPlanService, IStringLocalizer<AddPrescriptionRequest> t)
    {
        _treatmentPlanService = treatmentPlanService;
        _t = t;
    }

    public async Task<string> Handle(AddPrescriptionRequest request, CancellationToken cancellationToken)
    {
        await _treatmentPlanService.AddPrescription(request, cancellationToken);
        return _t["Success"];
    }
}