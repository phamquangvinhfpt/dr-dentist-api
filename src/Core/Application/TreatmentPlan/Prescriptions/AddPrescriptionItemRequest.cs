namespace FSH.WebApi.Application.TreatmentPlan.Prescriptions;

public class AddPrescriptionItemRequest
{
    public string? MedicineName { get; set; }
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
}

public class AddPrescriptionItemRequestValidator : CustomValidator<AddPrescriptionItemRequest>
{
    public AddPrescriptionItemRequestValidator()
    {
        RuleFor(p => p.MedicineName)
            .NotNull()
            .WithMessage("Medicine name is unavailable");

        RuleFor(p => p.Dosage)
            .NotNull()
            .WithMessage("Dosage is in unavailable");

        RuleFor(p => p.Frequency)
            .NotNull()
            .WithMessage("Frequency is unavailable");
    }
}