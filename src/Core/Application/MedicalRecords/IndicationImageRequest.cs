namespace FSH.WebApi.Application.MedicalRecords;
public class IndicationImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageType { get; set; } = string.Empty;
}
public class IndicationImageRequestValidator : CustomValidator<IndicationImageRequest>
{
    public IndicationImageRequestValidator()
    {
        RuleFor(x => x.ImageUrl)
            .NotEmpty()
            .WithMessage("Image URL is required");

        RuleFor(x => x.ImageType)
            .NotEmpty()
            .WithMessage("Image type is required");
    }
}
