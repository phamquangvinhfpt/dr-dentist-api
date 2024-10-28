using FSH.WebApi.Domain.Examination;

namespace FSH.WebApi.Domain.Treatment;

public class Prescription : AuditableEntity, IAggregateRoot
{
    public Guid? RecordId { get; set; }
    public string? Notes { get; set; }

    // navigation property
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public MedicalRecord? MedicalRecord { get; set; }

    public Prescription()
    {
    }

    public Prescription(Guid? recordId, string notes)
    {
        RecordId = recordId;
        Notes = notes;
    }
}