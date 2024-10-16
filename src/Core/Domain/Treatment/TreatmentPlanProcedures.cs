namespace FSH.WebApi.Domain.Treatment;

public class TreatmentPlanProcedures : AuditableEntity, IAggregateRoot
{
    public Guid? ProcedureId { get; set; }
    public Guid? DiagnosisId { get; set; }
    public int Quantity { get; set; }
    public TreatmentPlanStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Reason { get; set; }
    public string? RescheduledBy { get; set; }

    public TreatmentPlanProcedures()
    {
    }

    public TreatmentPlanProcedures(Guid procedureId, Guid diagnosisId, int quantity, TreatmentPlanStatus status, DateOnly? startDate, DateOnly? endDate, string? reason, string? rescheduledBy)
    {
        ProcedureId = procedureId;
        DiagnosisId = diagnosisId;
        Quantity = quantity;
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
        RescheduledBy = rescheduledBy;
    }
}
