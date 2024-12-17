using Microsoft.AspNetCore.Http;

namespace FSH.WebApi.Application.MedicalRecords;
public class IndicationImageRequest
{
    public IFormFile? Images { get; set; }
    public string ImageType { get; set; } = string.Empty;
}
public class IndicationImageRequestValidator : CustomValidator<IndicationImageRequest>
{
    public IndicationImageRequestValidator()
    {
        RuleFor(x => x.Images)
            .NotEmpty()
            .WithMessage("Image is required");

        RuleFor(x => x.ImageType)
            .NotEmpty()
            .WithMessage("Image type is required");
    }
}
