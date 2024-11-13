using FSH.WebApi.Domain.Treatment;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Service;

public class ServiceProcedures : AuditableEntity, IAggregateRoot
{
    public Guid? ServiceId { get; set; }
    public Guid? ProcedureId { get; set; }
    public int StepOrder { get; set; }

    // navigation properties
    [JsonIgnore]
    public ICollection<TreatmentPlanProcedures> TreatmentPlanProcedures { get; set; } = new List<TreatmentPlanProcedures>();

    public ServiceProcedures()
    {
    }

    public ServiceProcedures(Guid? ServiceId, Guid? ProcedureId, int stepOrder)
    {
        ServiceId = ServiceId;
        ProcedureId = ProcedureId;
        StepOrder = stepOrder;
    }
}