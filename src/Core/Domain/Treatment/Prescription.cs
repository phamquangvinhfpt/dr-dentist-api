using FSH.WebApi.Domain.Examination;

namespace FSH.WebApi.Domain.Treatment;

public class Prescription : AuditableEntity, IAggregateRoot
{
    public Guid? GeneralExaminationId { get; set; }
    public string? Notes { get; set; }

    // navigation property
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public GeneralExamination? GeneralExamination { get; set; }

    public Prescription()
    {
    }

    public Prescription(Guid? generalExaminationId, string notes)
    {
        GeneralExaminationId = generalExaminationId;
        Notes = notes;
    }
}