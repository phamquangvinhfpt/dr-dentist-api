
namespace FSH.WebApi.Domain.Examination;

public class PatientImage : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public Guid? IndicationId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageType { get; set; } = string.Empty;

    // navigation
    public Indication? Indication { get; set; }

    public PatientImage()
    {
    }

    public PatientImage(string? patientId, Guid? indicationId, string imageUrl, string imageType)
    {
        PatientId = patientId;
        IndicationId = indicationId;
        ImageUrl = imageUrl;
        ImageType = imageType;
    }
}