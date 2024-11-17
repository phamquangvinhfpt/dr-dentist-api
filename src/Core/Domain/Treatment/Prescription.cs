using FSH.WebApi.Domain.Examination;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Treatment;

public class Prescription : AuditableEntity, IAggregateRoot
{
    public Guid? TreatmentID { get; set; }
    public string? Notes { get; set; }

    // navigation property
    [JsonIgnore]
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    [JsonIgnore]
    public TreatmentPlanProcedures? TreatmentPlanProcedures { get; set; }

    public Prescription()
    {
    }

    public Prescription(Guid? planID, string notes)
    {
        TreatmentID = planID;
        Notes = notes;
    }
}