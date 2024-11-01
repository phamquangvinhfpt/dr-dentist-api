using FSH.WebApi.Domain.Service;
using FSH.WebApi.Domain.Treatment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FSH.WebApi.Domain.Payments;
public class PaymentDetail : AuditableEntity, IAggregateRoot
{
    public Guid TreatmentID { get; set; }
    public Guid PaymentID { get; set; }
    public Guid ProcedureID { get; set; }
    public DateOnly PaymentDay { get; set; }
    public double PaymentAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }

    //Navigation
    [JsonIgnore]
    public TreatmentPlanProcedures PlanProcedures { get; set; }
    [JsonIgnore]
    public Payment Payment { get; set; }
    [JsonIgnore]
    public Procedure Procedure { get; set; }

    public PaymentDetail()
    {
    }

    public PaymentDetail(Guid treatmentID, Guid paymentID, Guid procedureID, DateOnly paymentDay, double paymentAmount, PaymentStatus paymentStatus, TreatmentPlanProcedures planProcedures, Payment payment, Procedure procedure)
    {
        TreatmentID = treatmentID;
        PaymentID = paymentID;
        ProcedureID = procedureID;
        PaymentDay = paymentDay;
        PaymentAmount = paymentAmount;
        PaymentStatus = paymentStatus;
        PlanProcedures = planProcedures;
        Payment = payment;
        Procedure = procedure;
    }
}
