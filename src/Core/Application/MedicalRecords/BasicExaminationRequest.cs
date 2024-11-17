namespace FSH.WebApi.Application.MedicalRecords;
public class BasicExaminationRequest
{
    public string? ExaminationContent { get; set; }
    public string? TreatmentPlanNote { get; set; }
}
public class BasicExaminationValidator : CustomValidator<BasicExaminationRequest>
{
    public BasicExaminationValidator()
    {
        RuleFor(x => x.ExaminationContent)
            .NotEmpty()
            .WithMessage("Exam content is required");

        RuleFor(x => x.TreatmentPlanNote)
           .NotEmpty()
           .WithMessage("Treatment plan note is required");
    }
}
