using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Examination;

public class Indication : AuditableEntity, IAggregateRoot
{
    public Guid? RecordId { get; set; }
    public string[] IndicationType { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = string.Empty;

    // navigation
    [JsonIgnore]
    public ICollection<PatientImage> Images { get; set; } = new List<PatientImage>();
    public MedicalRecord MedicalRecord { get; set; }
    public Indication()
    {
    }

    public Indication(Guid? recordId, string[] indicationType, string description)
    {
        RecordId = recordId;
        IndicationType = indicationType;
        Description = description;
    }
}