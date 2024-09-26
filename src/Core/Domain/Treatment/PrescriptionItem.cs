namespace FSH.WebApi.Domain.Treatment;

public class PrescriptionItem : AuditableEntity, IAggregateRoot
{
    public Guid? PrescriptionId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;

    public PrescriptionItem()
    {
    }

    public PrescriptionItem(Guid? prescriptionId, string medicineName, string dosage, string frequency)
    {
        PrescriptionId = prescriptionId;
        MedicineName = medicineName;
        Dosage = dosage;
        Frequency = frequency;
    }
}