using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Examination;

namespace FSH.WebApi.Domain.Payments;

public class Payment : AuditableEntity, IAggregateRoot
{
    public Guid? GeneralExaminationId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }

    public GeneralExamination? GeneralExamination { get; set; }

    public Payment()
    {
    }

    public Payment(Guid? generalExaminationId, decimal amount, PaymentMethod method, PaymentStatus status)
    {
        GeneralExaminationId = generalExaminationId;
        Amount = amount;
        Method = method;
        Status = status;
    }
}
