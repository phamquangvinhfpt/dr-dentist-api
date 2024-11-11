using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Treatment;

public class TreatmentPlanProcedures : AuditableEntity, IAggregateRoot
{
    public Guid? ServiceProcedureId { get; set; }
    public Guid? RecordId { get; set; }
    public Guid? DoctorID { get; set; }
    public TreatmentPlanStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeSpan? EndDate { get; set; }
    public string? Reason { get; set; }
    public string? RescheduledBy { get; set; }

    // navigation properties
    [JsonIgnore]
    public MedicalRecord? MedicalRecord { get; set; }
    [JsonIgnore]
    public ServiceProcedures? ServiceProcedure { get; set; }
    [JsonIgnore]
    public PaymentDetail? PaymentDetail { get; set; }
    public TreatmentPlanProcedures()
    {
    }

    public TreatmentPlanProcedures(DefaultIdType? serviceProcedureId, Guid? recordId, Guid? doctorID, TreatmentPlanStatus status, DateOnly? startDate, TimeSpan? endDate, string? reason, string? rescheduledBy)
    {
        ServiceProcedureId = serviceProcedureId;
        RecordId = recordId;
        DoctorID = doctorID;
        Status = status;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
        RescheduledBy = rescheduledBy;
    }
}
