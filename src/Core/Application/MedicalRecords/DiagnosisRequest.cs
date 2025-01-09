namespace FSH.WebApi.Application.MedicalRecords;
public class DiagnosisRequest
{
    public int ToothNumber { get; set; }
    public string[] TeethConditions { get; set; } = Array.Empty<string>();
}
public class DiagnosisRequestValidator : CustomValidator<DiagnosisRequest>
{
    public DiagnosisRequestValidator(IMedicalRecordService medicalRecordService)
    {
        RuleFor(x => x.ToothNumber)
            .NotEmpty()
            .WithMessage("Tooth number is required")
            .MustAsync(async (i, _) => await medicalRecordService.CheckToothNumberValidAsync(i))
            .WithMessage((_, i) => $"Invalid tooth number. Tooth number at: {i}");

        RuleFor(x => x.TeethConditions)
            .NotNull()
            .WithMessage("Teeth condition is required");
    }
}