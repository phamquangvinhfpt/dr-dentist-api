namespace FSH.WebApi.Application.MedicalRecords;
public class IndicationRequest
{
    public string[] IndicationType { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = string.Empty;
}
public class IndicationRequestValidator : CustomValidator<IndicationRequest>
{
    public IndicationRequestValidator()
    {
        RuleFor(x => x.IndicationType)
            .NotEmpty()
            .WithMessage("Indication type is required");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required");
    }
}
