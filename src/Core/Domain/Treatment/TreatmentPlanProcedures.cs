using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;

namespace FSH.WebApi.Domain.Treatment;

public class TreatmentPlanProcedures : AuditableEntity, IAggregateRoot
{
    public Guid? ServiceProcedureId { get; set; }
    public Guid? RecordId { get; set; }
    public int Quantity { get; set; }
    public TreatmentPlanStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reason { get; set; }
    public string? RescheduledBy { get; set; }

    // navigation properties
    public MedicalRecord? MedicalRecord { get; set; }
    public ServiceProcedures? ServiceProcedure { get; set; }
    public PaymentDetail? PaymentDetail { get; set; }
    public TreatmentPlanProcedures()
    {
    }

    public TreatmentPlanProcedures(Guid? serviceProcedureId, Guid? recordId, int quantity, TreatmentPlanStatus status, DateOnly? startDate, DateOnly? endDate, string? reason, string? rescheduledBy)
    {
        ServiceProcedureId = serviceProcedureId;
        RecordId = recordId;
        Quantity = quantity;
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
        RescheduledBy = rescheduledBy;
    }
}
