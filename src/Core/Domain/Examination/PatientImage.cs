
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Examination;

public class PatientImage : AuditableEntity, IAggregateRoot
{
    public Guid? IndicationId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageType { get; set; } = string.Empty;

    // navigation
    [JsonIgnore]
    public Indication? Indication { get; set; }

    public PatientImage()
    {
    }

    public PatientImage(Guid? indicationId, string imageUrl, string imageType)
    {
        IndicationId = indicationId;
        ImageUrl = imageUrl;
        ImageType = imageType;
    }
}