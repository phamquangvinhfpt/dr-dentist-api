using FSH.WebApi.Domain.Service;

namespace FSH.WebApi.Domain.Treatment;

public class TreatmentPlanProcedures : AuditableEntity, IAggregateRoot
{
    public Guid TreatmentPlanId { get; set; }
    public Guid ProcedureId { get; set; }
    public int Quantity { get; set; }
    public TreatmentPlanStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reason { get; set; }
    public string? RescheduledBy { get; set; }

    public TreatmentPlanProcedures()
    {
    }

    public TreatmentPlanProcedures(Guid treatmentPlanId, Guid procedureId, int quantity, TreatmentPlanStatus status, DateOnly? startDate, DateOnly? endDate, string? reason, string? rescheduledBy)
    {
        TreatmentPlanId = treatmentPlanId;
        ProcedureId = procedureId;
        Quantity = quantity;
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
        RescheduledBy = rescheduledBy;
    }
}
