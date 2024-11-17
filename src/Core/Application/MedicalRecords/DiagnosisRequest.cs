namespace FSH.WebApi.Application.MedicalRecords;
public class DiagnosisRequest
{
    public int ToothNumber { get; set; }
    public string[] TeethConditions { get; set; } = Array.Empty<string>();
}
public class DiagnosisRequestValidator : CustomValidator<DiagnosisRequest>
{
    public DiagnosisRequestValidator()
    {
        RuleFor(x => x.ToothNumber)
            .NotEmpty()
            .WithMessage("Tooth number is required")
            .InclusiveBetween(1, 32)
            .WithMessage("Tooth number must be between 1 and 32");

        RuleFor(x => x.TeethConditions)
            .NotEmpty()
            .WithMessage("Teeth condition is required");
    }
}